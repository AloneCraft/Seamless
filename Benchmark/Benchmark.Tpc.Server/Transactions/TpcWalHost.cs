using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Benchmark.Tpc.Core;
using Seamless.Execution;
using Seamless.Transactions;
namespace Benchmark.Tpc.Server;
public readonly record struct TpcDurableResult<T>(T Result,Guid TransactionId,long FenceLsn);
/// <summary>TPCの既存Undo/行codecを製品トランザクション基盤へ接続する。</summary>
public sealed class TpcWalHost:IAsyncDisposable{
    private static readonly AsyncLocal<Capture?> Current=new();
    private readonly TpcDatabaseBase[] databases;
    private readonly Dictionary<(int,int),ITableView> tables=[];
    private readonly PartitionCheckpoint[] checkpoints;
    private readonly IWriteObserver?[] previous;
    private readonly IWriteObserver[] observers;
    private readonly WalWriter writer;
    private readonly TransactionJournal? journal;
    private readonly ResourceBudget budget;
    private readonly PartitionExecutor scheduler;
    private readonly long[] saved;
    private readonly HashSet<ITableView> readOnly;
    public TpcWalHost(IReadOnlyList<TpcDatabaseBase> databases,WalWriter writer,TransactionJournal? journal=null,IReadOnlyCollection<ITableView>? readOnlyTables=null):this(databases,writer,journal,readOnlyTables,null){}
    public TpcWalHost(IReadOnlyList<TpcDatabaseBase> databases,WalWriter writer,TransactionJournal? journal,IReadOnlyCollection<ITableView>? readOnlyTables,Action<WalEntry>? replayApplied){
        this.databases=databases.ToArray();this.writer=writer;this.journal=journal;
        this.readOnly=new(readOnlyTables??[],ReferenceEqualityComparer.Instance);
        this.budget=journal?.Budget??new ResourceBudget(256L<<20,16L<<30);
        if(this.databases.Length==0||this.databases.Select(static d=>d.Store).Distinct(ReferenceEqualityComparer.Instance).Count()!=this.databases.Length){throw new ArgumentException("One independent Store per partition is required.");}
        this.checkpoints=new PartitionCheckpoint[this.databases.Length];this.saved=new long[this.databases.Length];
        this.previous=new IWriteObserver?[this.databases.Length];this.observers=new IWriteObserver[this.databases.Length];
        var id=journal?.Device.WalId??Guid.NewGuid();
        PartitionExecutor? initializedScheduler=null;
        try{
        for(var part=0;part<this.databases.Length;part++){
            var database=this.databases[part];
            if(database.Log.Observer is not null){throw new InvalidOperationException("A TPC observer is already attached. Do not combine C10/v1 recorders with this host.");}
            foreach(var table in database.Tables){this.tables.Add((part,table.TableId),table);}
            var checkpoint=new PartitionCheckpoint(part,id);database.Store.Register(checkpoint);this.checkpoints[part]=checkpoint;
            if(database.Store.CurrentCheckpoint is not null&&!checkpoint.Loaded){throw new InvalidDataException("Legacy Store detected; construct the new SUT in fresh stores.");}
            if(checkpoint.WalId!=id||checkpoint.Partition!=part){throw new InvalidDataException("TPC checkpoint/WAL identity mismatch.");}
            this.saved[part]=checkpoint.Lsn;
        }
        if(this.checkpoints.Any(static c=>c.Loaded)&&this.checkpoints.Any(static c=>!c.Loaded)){throw new InvalidDataException("Incomplete initial dataset.");}
        if(journal is not null&&!this.checkpoints.Any(static c=>c.Loaded)){
            if(journal.Device.RecoveredLsn!=0){throw new InvalidDataException("A nonempty WAL requires the original initialized Store checkpoints.");}
            foreach(var database in this.databases){database.ReleaseHandles();database.Store.Save();database.AcquireHandles();}
        }
        var applied=this.saved.ToArray();
        if(journal is not null){
            if(this.saved.Any(lsn=>lsn>journal.Device.RecoveredLsn||lsn<journal.Device.FirstAvailableLsn-1)){throw new InvalidDataException("Partition checkpoint is outside retained WAL coverage.");}
            try{
                foreach(var record in journal.ReadTransactions()){
                    if(record.Draft.Partitions.Any(part=>part<0||part>=this.databases.Length)){throw new InvalidDataException("Unknown WAL partition.");}
                    var decoded=record.Draft.Entries.Select(this.Validate).ToArray();
                    foreach(var item in decoded){if(record.Lsn>this.saved[item.Entry.Partition]){item.Table.ApplyRedoBoxed(item.Kind,item.Key,item.Row);replayApplied?.Invoke(item.Entry);}}
                    foreach(var part in record.Draft.Partitions){applied[part]=Math.Max(applied[part],record.Lsn);}
                }
            }catch(Exception error){writer.Fail(error);throw;}
        }
        this.scheduler=initializedScheduler=new(writer,applied,draft=>{
            if(journal is not null){return journal.AppendAsync(draft);}
            return writer.AppendAsync(draft);
        });
        for(var part=0;part<this.databases.Length;part++){
            this.previous[part]=this.databases[part].Log.Observer;this.observers[part]=new Observer(this,part);
            this.databases[part].Log.Observer=this.observers[part];
        }
        }catch(Exception error){
            writer.Fail(error);initializedScheduler?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            for(var part=0;part<this.databases.Length;part++){if(this.checkpoints[part] is not null){this.databases[part].Store.Unregister(this.checkpoints[part]);}}
            throw;
        }
    }
    public bool AuditMode{get;init;}
    public TransactionFailure Failure=>this.writer.Failure;
    public long AppliedLsn(int partition)=>this.scheduler.AppliedLsn(partition);
    public async Task<TpcDurableResult<T>> ExecuteAsync<T>(int[] partitions,Func<T> execute,Func<T,bool> rolledBack,Func<T,byte[],byte[]> metadata,CancellationToken cancellationToken=default){
        var parts=partitions.Distinct().Order().ToArray();var id=Guid.NewGuid();var began=false;
        try{
        var result=await this.scheduler.ExecuteAsync(parts,()=>{
            began=true;
            if(Current.Value is not null){throw new InvalidOperationException("Nested TPC execution capture is not allowed.");}
            using var capture=new Capture(this,this.budget,parts);Current.Value=capture;
            try{
                var value=execute();var rollback=rolledBack(value)||capture.HadUndo;WalDraft? draft=null;
                IReadOnlyList<WalEntry> entries=capture.Entries;
                if(rollback){entries=[];this.journal?.RecordAborted(id);}
                if(entries.Count>0||this.AuditMode){draft=new(id,parts,entries,metadata(value,capture.Hash.GetHashAndReset()));}
                IDisposable? reservation=null;if(draft is not null){reservation=capture.TransferMemory();}
                return new PartitionExecution<T>(value,draft,reservation);
            }finally{Current.Value=null;}
        },cancellationToken).ConfigureAwait(false);
        return new(result.Result,id,result.FenceLsn);
        }catch(OperationCanceledException) when(!began&&cancellationToken.IsCancellationRequested){throw;}
        catch(Exception error){
            var outcome=TransactionOutcomeKind.Unknown;
            try{if(this.journal is not null){outcome=this.journal.GetOutcome(id).Kind;}}catch(Exception lookupError){error.Data["outcome-lookup-failure"]=lookupError.Message;}
            throw new TransactionCommitException(id,outcome,error);
        }
    }
    public async Task CheckpointAsync(int partition,CancellationToken cancellationToken=default){
        try{
        await this.scheduler.CheckpointAsync(partition,lsn=>{
            var database=this.databases[partition];this.checkpoints[partition].Lsn=lsn;
            database.ReleaseHandles();database.Store.Save();database.AcquireHandles();Interlocked.Exchange(ref this.saved[partition],lsn);
        },cancellationToken).ConfigureAwait(false);
        if(this.journal is not null){await this.journal.CheckpointAndPruneAsync(this.saved.Min()).ConfigureAwait(false);}
        }catch(Exception error){this.writer.Fail(error);throw;}
    }
    private static string Codec(ITableView table)=>TpcCodecIdentity.For(table);
    private (WalEntry Entry,ITableView Table,WriteKind Kind,object Key,object? Row) Validate(WalEntry entry){
        if(!this.tables.TryGetValue((entry.Partition,entry.TableId),out var table)||entry.Codec!=Codec(table)){throw new InvalidDataException("TPC row codec/catalog mismatch.");}
        if(this.readOnly.Contains(table)){throw new InvalidDataException("WAL attempts to modify a replicated read-only table.");}
        var key=table.DecodeKey(entry.Key);
        if(!table.EncodeKey(key).AsSpan().SequenceEqual(entry.Key)){throw new InvalidDataException("TPC key length/schema mismatch.");}
        object? row=null;if(entry.Row is not null){row=table.DecodeRow(entry.Row);if(!Equals(key,table.KeyOf(row))||!table.EncodeRow(row).AsSpan().SequenceEqual(entry.Row)){throw new InvalidDataException("TPC row key/length/schema mismatch.");}}
        var kind=entry.Mutation switch{WalMutation.Insert=>WriteKind.Insert,WalMutation.Update=>WriteKind.Update,WalMutation.Delete=>WriteKind.Delete,_=>throw new InvalidDataException("Unsupported TPC mutation.")};
        return(entry,table,kind,key,row);
    }
    public async ValueTask DisposeAsync(){
        try{await this.scheduler.DisposeAsync().ConfigureAwait(false);}
        finally{for(var part=0;part<this.databases.Length;part++){if(ReferenceEquals(this.databases[part].Log.Observer,this.observers[part])){this.databases[part].Log.Observer=this.previous[part];}this.databases[part].Store.Unregister(this.checkpoints[part]);}}
    }
    private sealed class Observer(TpcWalHost owner,int partition):IWriteObserver{
        public void OnWrite(int tableId,WriteKind kind,object key,object? row){
            try{this.CaptureWrite(tableId,kind,key,row);}catch(Exception error){owner.writer.Fail(error);throw;}
        }
        private void CaptureWrite(int tableId,WriteKind kind,object key,object? row){
            var capture=Current.Value;
            if(capture is null||!ReferenceEquals(capture.Owner,owner)){throw new InvalidOperationException("TPC writes must run inside an owned transaction capture.");}
            var table=owner.tables[(partition,tableId)];
            if(!capture.Partitions.Contains(partition)||owner.readOnly.Contains(table)){throw new InvalidOperationException("Mutation is outside the owned writable partition/table set.");}
            var keyBytes=table.EncodeKey(key);byte[]? rowBytes=null;if(row is not null){rowBytes=table.EncodeRow(row);}
            var size=checked(keyBytes.Length+(rowBytes?.Length??0)+128);capture.Memory.Resize(checked(capture.Memory.Bytes+size));
            if(capture.Memory.Bytes>4L<<20){throw new InvalidDataException("TPC transaction exceeds WAL frame bound.");}
            Span<byte> header=stackalloc byte[20];BinaryPrimitives.WriteInt32LittleEndian(header,partition);BinaryPrimitives.WriteInt32LittleEndian(header[4..],tableId);BinaryPrimitives.WriteInt32LittleEndian(header[8..],(int)kind);BinaryPrimitives.WriteInt32LittleEndian(header[12..],keyBytes.Length);BinaryPrimitives.WriteInt32LittleEndian(header[16..],rowBytes?.Length??-1);
            capture.Hash.AppendData(header);capture.Hash.AppendData(keyBytes);if(rowBytes is not null){capture.Hash.AppendData(rowBytes);}
            if(kind is WriteKind.Insert or WriteKind.Update or WriteKind.Delete){
                var mutation=kind switch{WriteKind.Insert=>WalMutation.Insert,WriteKind.Update=>WalMutation.Update,_=>WalMutation.Delete};
                capture.Entries.Add(new(partition,tableId,mutation,Codec(table),keyBytes,rowBytes));
            }else{capture.HadUndo=true;}
        }
    }
    private sealed class Capture(TpcWalHost owner,ResourceBudget budget,int[] partitions):IDisposable{
        public TpcWalHost Owner{get;}=owner;
        public int[] Partitions{get;}=partitions;
        public List<WalEntry> Entries{get;}=[];
        public bool HadUndo;
        public ResourceReservation Memory{get;}=budget.ReserveMemory(0);
        public IncrementalHash Hash{get;}=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private bool transferred;
        public IDisposable TransferMemory(){this.transferred=true;return this.Memory;}
        public void Dispose(){this.Hash.Dispose();if(!this.transferred){this.Memory.Dispose();}}
    }
    private sealed class PartitionCheckpoint(int partition,Guid walId):IStoreComponent{
        public string Name=>"~partition-checkpoint-v2";
        public int Partition{get;private set;}=partition;
        public Guid WalId{get;private set;}=walId;
        public long Lsn{get;set;}
        public bool Loaded{get;private set;}
        public string Save()=>JsonSerializer.Serialize(new State(2,this.Partition,this.WalId,this.Lsn));
        public void Load(string state){var value=JsonSerializer.Deserialize<State>(state)??throw new InvalidDataException("Missing checkpoint.");if(value.Version!=2||value.Lsn<0){throw new InvalidDataException("Invalid TPC checkpoint.");}this.Partition=value.Partition;this.WalId=value.WalId;this.Lsn=value.Lsn;this.Loaded=true;}
        public void Flush(){}
        public void Dispose(){}
        private sealed record State(int Version,int Partition,Guid WalId,long Lsn);
    }
}
