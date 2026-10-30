using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;
using Seamless.Transactions;
using Seamless.Transactions.Collections;
using ExecutionStore=Seamless.Execution.Store;
namespace Seamless.Database;
public abstract partial class DatabaseContainer{
    private ExecutionStore? sharedStore;
    private TransactionJournal? sharedJournal;
    private TransactionCoordinator? sharedCoordinator;
    private readonly SharedCheckpoint sharedCheckpoint=new();
    private readonly Dictionary<string,ITransactionParticipant> sharedParticipants=new(StringComparer.Ordinal);
    private readonly List<(Action Release,Action Acquire)> sharedHandles=[];
    private bool sharedReady;
    protected virtual string TransactionCatalogIdentity=>this.GetType().FullName??this.DatabaseName;
    protected string TransactionCodecIdentity<TKey,TRecord>(string name)=>name+"|"+this.executionResources.Codecs.Get<TKey>().FormatId+"|"+this.executionResources.Codecs.Get<TRecord>().FormatId;
    protected virtual void EnsureTransactionSchema(){throw new NotSupportedException("SharedV2 requires a generated typed transaction schema.");}
    protected ValueTask<TransactionScope> BeginTransactionScopeAsync(CancellationToken cancellationToken=default){
        this.EnsureAvailable();
        if(this.Options.Transactions!=DatabaseTransactionMode.SharedV2){throw new InvalidOperationException("Select SharedV2 explicitly on a new database; legacy databases are not automatically migrated.");}
        if(this.ambientReadSession.Value is not null||this.ambientTransaction.Value is not null){throw new InvalidOperationException("A legacy/read scope is already active.");}
        this.EnsureSharedReady();return this.sharedCoordinator!.BeginAsync(cancellationToken);
    }
    private void EnsureSharedReady(){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.Options.Transactions!=DatabaseTransactionMode.SharedV2){throw new InvalidOperationException("Shared transaction maintenance requires SharedV2. Legacy databases are not automatically migrated.");}
        lock(this.gate){
            if(this.sharedReady){return;}
            this.EnsureTransactionSchema();this.EnsureSharedStorage();
            if(this.sharedStore!.Kind==StoreKind.Persistent&&this.sharedStore.CurrentCheckpoint is null){
                if(this.sharedJournal!.Device.RecoveredLsn!=0){throw new InvalidDataException("Committed WAL requires the original initialized Store checkpoint.");}
                try{
                    foreach(var handle in this.sharedHandles){handle.Release();}
                    this.sharedStore.Save();foreach(var handle in this.sharedHandles){handle.Acquire();}
                }catch(Exception error){this.sharedCoordinator!.Failure.Fail(error);this.sharedJournal.Writer.Fail(error);throw;}
            }
            this.sharedCoordinator!.Recover(this.sharedCheckpoint.Lsn);this.sharedReady=true;
        }
    }
    private void EnsureSharedStorage(){
        if(this.sharedStore is not null){return;}
        var failure=new TransactionFailure();
        try{
            if(this.Options.StorageMode==DatabaseStorageMode.Storage){
                var root=this.ResolveStorageRoot();
                if(File.Exists(Path.Combine(root,"database.wal"))||Directory.Exists(root)&&Directory.EnumerateFiles(root,"checkpoint.token",SearchOption.AllDirectories).Any()){
                    throw new InvalidOperationException("Legacy database detected. Create SharedV2 in a separate new directory; automatic migration is disabled.");
                }
                this.sharedJournal=new(Path.Combine(root,"wal-v2"),new WalOptions{CatalogIdentity=this.TransactionCatalogIdentity},this.executionResources.Budget,failure);
                this.sharedStore=ExecutionStore.Open(Path.Combine(root,"store-v2"),parentBudget:this.executionResources.Budget);
                this.sharedStore.Register(this.sharedCheckpoint);
                if(this.sharedCheckpoint.Loaded&&this.sharedCheckpoint.WalId!=this.sharedJournal.Device.WalId){throw new InvalidDataException("Store and WAL belong to different datasets.");}
                if(this.sharedStore.CurrentCheckpoint is not null&&!this.sharedCheckpoint.Loaded){throw new InvalidDataException("SharedV2 checkpoint component is missing.");}
                this.sharedCheckpoint.WalId=this.sharedJournal.Device.WalId;
            }else{this.sharedStore=ExecutionStore.CreateMemory(parentBudget:this.executionResources.Budget);}
            this.sharedCoordinator=new(this.sharedJournal,failure);
        }catch{this.sharedStore?.Dispose();this.sharedJournal?.DisposeAsync().AsTask().GetAwaiter().GetResult();this.sharedStore=null;this.sharedJournal=null;throw;}
    }
    private Table<TKey,TRecord> CreateSharedTable<TKey,TRecord>(Func<TRecord,TKey> keySelector,string name) where TKey:struct where TRecord:struct{
        this.EnsureSharedStorage();var sizing=this.ResolveTableStorage(name);
        var data=this.sharedStore!.CreateTsavorite<TKey,TRecord>(name,new TsavoriteTuning{
            IndexSize=checked(sizing.IndexSize*64),PageSize=sizing.PageSize,SegmentSize=sizing.SegmentSize,LogMemorySize=sizing.MemorySize,
        });
        var handle=data.Create();
        var table=new Table<TKey,TRecord>(this,handle,keySelector,new SharedEmptyLease(),name,data.Create);
        var id=BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)))&int.MaxValue;
        if(id==0){id=1;}
        var identity=name+"|"+this.executionResources.Codecs.Get<TKey>().FormatId+"|"+this.executionResources.Codecs.Get<TRecord>().FormatId;
        var participant=new TransactionalTable<TKey,TRecord>(0,id,identity,this.executionResources,this.Options.TransactionMemoryLimit,
            key=>{var found=table.DirectTryGetValue(key,out var row);return(found,row);},table.DirectSet,key=>table.DirectRemove(key),keySelector,()=>table.DirectRows);
        this.sharedCoordinator!.Register(participant);this.sharedParticipants.Add(name,participant);
        this.sharedHandles.Add((table.ReleaseSharedHandle,table.AcquireSharedHandle));return table;
    }
    public TransactionCollection<TKey,TRecord> GetTransactionCollection<TKey,TRecord>(TransactionScope scope,Table<TKey,TRecord> table) where TKey:struct where TRecord:struct{
        this.EnsureAvailable();
        if(!this.tables.TryGetValue(table.Name,out var own)||!ReferenceEquals(own,table)||!this.sharedParticipants.TryGetValue(table.Name,out var participant)||participant is not TransactionalTable<TKey,TRecord> typed){throw new InvalidOperationException("Table is not part of this typed transaction schema.");}
        return typed.For(scope);
    }
    private MaterializedView<TKey,TRecord> CreateSharedMaterializedView<TKey,TRecord>(Func<TRecord,TKey> keySelector,string name) where TKey:struct where TRecord:struct{
        this.EnsureSharedStorage();var sizing=this.ResolveTableStorage(name);
        var data=this.sharedStore!.CreateTsavorite<TKey,TRecord>(name,new TsavoriteTuning{IndexSize=checked(sizing.IndexSize*64),PageSize=sizing.PageSize,SegmentSize=sizing.SegmentSize,LogMemorySize=sizing.MemorySize});
        var view=new MaterializedView<TKey,TRecord>(this,data.Create(),keySelector,new SharedEmptyLease(),name,data.Create);
        var id=BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)))&int.MaxValue;if(id==0){id=1;}
        var participant=new TransactionalTable<TKey,TRecord>(0,id,this.TransactionCodecIdentity<TKey,TRecord>(name),this.executionResources,this.Options.TransactionMemoryLimit,
            key=>{var found=view.DirectTryGetValue(key,out var row);return(found,row);},view.DirectSet,static _=>throw new InvalidOperationException("Materialized views are read-only."),keySelector,()=>view.DirectRows);
        this.sharedCoordinator!.Register(participant);this.sharedParticipants.Add(name,participant);this.sharedHandles.Add((view.ReleaseSharedHandle,view.AcquireSharedHandle));return view;
    }
    public ReadOnlyTransactionCollection<TKey,TRecord> GetTransactionCollection<TKey,TRecord>(TransactionScope scope,MaterializedView<TKey,TRecord> view) where TKey:struct where TRecord:struct{
        this.EnsureAvailable();
        if(!this.tables.TryGetValue(view.Name,out var own)||!ReferenceEquals(own,view)||!this.sharedParticipants.TryGetValue(view.Name,out var participant)||participant is not TransactionalTable<TKey,TRecord> typed){throw new InvalidOperationException("View is not part of this typed transaction schema.");}
        return new(typed.For(scope));
    }
    private Table<TRecord> CreateSharedTable<TRecord>(string name) where TRecord:notnull{
        this.EnsureSharedStorage();var sizing=this.ResolveTableStorage(name);
        var data=this.sharedStore!.CreateTsavoriteHashSet<TRecord>(name,new TsavoriteTuning{IndexSize=checked(sizing.IndexSize*64),PageSize=sizing.PageSize,SegmentSize=sizing.SegmentSize,LogMemorySize=sizing.MemorySize});
        var table=new Table<TRecord>(this,data.Create(),name,data.Create);
        var id=BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)))&int.MaxValue;if(id==0){id=1;}
        var participant=new TransactionalTable<TRecord,TRecord>(0,id,this.TransactionCodecIdentity<TRecord,TRecord>(name),this.executionResources,this.Options.TransactionMemoryLimit,
            key=>(table.DirectContains(key),key),(key,row)=>table.DirectAdd(row),key=>table.DirectRemove(key),static row=>row,()=>table.DirectRows.Select(static row=>new KeyValuePair<TRecord,TRecord>(row,row)));
        this.sharedCoordinator!.Register(participant);this.sharedParticipants.Add(name,participant);this.sharedHandles.Add((table.ReleaseSharedHandle,table.AcquireSharedHandle));return table;
    }
    public TransactionCollection<TRecord,TRecord> GetTransactionCollection<TRecord>(TransactionScope scope,Table<TRecord> table) where TRecord:notnull{
        this.EnsureAvailable();
        if(!this.tables.TryGetValue(table.Name,out var own)||!ReferenceEquals(own,table)||!this.sharedParticipants.TryGetValue(table.Name,out var participant)||participant is not TransactionalTable<TRecord,TRecord> typed){throw new InvalidOperationException("Table is not part of this typed transaction schema.");}
        return typed.For(scope);
    }
    public ValueTask<TransactionOutcome> GetTransactionOutcomeAsync(Guid id){
        this.EnsureSharedReady();return this.sharedCoordinator!.GetOutcomeAsync(id);
    }
    public ValueTask ForgetTransactionOutcomesThroughAsync(long lsn){
        this.EnsureSharedReady();
        if(this.sharedJournal is null){return ValueTask.CompletedTask;}
        return this.sharedJournal.ForgetThroughAsync(lsn);
    }
    public async ValueTask CheckpointTransactionsAsync(){
        this.EnsureSharedReady();using var lease=this.sharedCoordinator!.EnterRead();
        await this.SaveShared().ConfigureAwait(false);
    }
    private async ValueTask SaveShared(){
        if(this.sharedStore!.Kind!=StoreKind.Persistent){return;}
        this.sharedCoordinator!.Failure.ThrowIfFailed();var lsn=this.sharedCoordinator.AppliedLsn;
        try{
            await this.sharedJournal!.Writer.WaitForDurableAsync(lsn).ConfigureAwait(false);
            this.sharedCheckpoint.Lsn=lsn;
            foreach(var handle in this.sharedHandles){handle.Release();}
            this.sharedStore.Save();
            foreach(var handle in this.sharedHandles){handle.Acquire();}
            await this.sharedJournal.CheckpointAndPruneAsync(lsn).ConfigureAwait(false);
        }catch(Exception error){this.sharedCoordinator.Failure.Fail(error);this.sharedJournal!.Writer.Fail(error);throw;}
    }
    private void DisposeShared(){
        if(this.ambientTransaction.Value is not null||this.ambientReadSession.Value is not null||this.sharedCoordinator?.HasCurrentTransaction==true){throw new InvalidOperationException("Finish active transactions/read sessions before disposing the database.");}
        var errors=new List<Exception>();
        this.sharedCoordinator?.StopAccepting();
        if(this.sharedCoordinator is not null){
            using var lease=this.sharedCoordinator.EnterReadForShutdown();
            if(this.sharedCoordinator.Failure.Error is null){try{this.SaveShared().AsTask().GetAwaiter().GetResult();}catch(Exception error){errors.Add(error);}}
            this.disposed=true;
            foreach(var table in this.tables.Values){try{table.Dispose();}catch(Exception error){errors.Add(error);}}
            this.tables.Clear();
        }else{this.disposed=true;}
        try{this.sharedCoordinator?.DisposeAsync().AsTask().GetAwaiter().GetResult();}catch(Exception error){errors.Add(error);}
        try{this.sharedStore?.Dispose();}catch(Exception error){errors.Add(error);}
        try{this.sharedJournal?.DisposeAsync().AsTask().GetAwaiter().GetResult();}catch(Exception error){errors.Add(error);}
        try{this.executionResources.Dispose();}catch(Exception error){errors.Add(error);}
        if(errors.Count>0){throw new AggregateException("Shared transaction database cleanup failed.",errors);}
    }
    private sealed class SharedEmptyLease:IDisposable{public void Dispose(){}}
    private sealed class SharedScopeLease(TransactionScope scope):IDisposable{public void Dispose()=>scope.DisposeAsync().AsTask().GetAwaiter().GetResult();}
    private sealed class SharedLegacyChanges(DatabaseContainer owner,ITransactionOverlay[] overlays):ITransactionChanges{
        private readonly ResourceReservation walMemory=owner.executionResources.Budget.ReserveMemory(0);
        public IReadOnlyList<WalEntry> Encode()=>this.EncodeBounded(int.MaxValue);
        public IReadOnlyList<WalEntry> EncodeBounded(int maximumBytes){
            var entries=new List<WalEntry>();long bytes=0;this.walMemory.Resize(0);
            foreach(var overlay in overlays){
                foreach(var operation in overlay.CreateWalOperations()){
                    using(operation){
                        var participant=owner.sharedParticipants[operation.TableName];byte[]? row=null;var kind=WalMutation.Upsert;
                        if(operation.Deleted){kind=WalMutation.Delete;}else{row=Convert.FromBase64String(operation.RecordData!);}
                        var key=Convert.FromBase64String(operation.KeyData);bytes=checked(bytes+WalFormat.EntryPayloadBytes(participant.CodecIdentity,key.Length,row?.Length??0));
                        if(bytes>maximumBytes){throw new InvalidDataException("Transaction exceeds the WAL frame bound.");}
                        this.walMemory.Resize(checked(this.walMemory.Bytes+key.Length+(row?.Length??0)+128L));
                        entries.Add(new(participant.Partition,participant.TableId,kind,participant.CodecIdentity,key,row));
                    }
                }
            }
            return entries;
        }
        public void Apply(){foreach(var overlay in overlays){overlay.Apply();}}
        public void Dispose()=>this.walMemory.Dispose();
    }
    private sealed class SharedCheckpoint:IStoreComponent{
        public string Name=>"~transaction-checkpoint-v2";
        public Guid WalId;
        public long Lsn;
        public bool Loaded;
        public string Save()=>global::System.Text.Json.JsonSerializer.Serialize(new CheckpointData(2,this.WalId,this.Lsn));
        public void Load(string state){
            var value=global::System.Text.Json.JsonSerializer.Deserialize<CheckpointData>(state)??throw new InvalidDataException("Missing transaction checkpoint.");
            if(value.Version!=2||value.WalId==Guid.Empty||value.Lsn<0){throw new InvalidDataException("Invalid transaction checkpoint.");}
            this.WalId=value.WalId;this.Lsn=value.Lsn;this.Loaded=true;
        }
        public void Flush(){}
        public void Dispose(){}
        private sealed record CheckpointData(int Version,Guid WalId,long Lsn);
    }
}
