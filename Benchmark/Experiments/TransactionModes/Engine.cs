using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Seamless.Collections.Serializers;
using Seamless.Transactions;

namespace TransactionModes;

public sealed class Engine:IDisposable{
    private readonly object gate=new();
    private readonly SemaphoreSlim single=new(1,1);
    private readonly ConcurrentDictionary<long,State> states=new();
    private readonly Dictionary<Key,long> reservations=[];
    private readonly ConcurrentDictionary<Key,long> committed=new();
    private readonly long[] epochs=[0,0];
    private readonly List<Tx> active=[];
    private readonly ThreadLocal<Client> clients=new(static()=>new Client(),true);
    private int generation;
    private sealed class Client{public int Generation=-1;public Access[] Accesses=[];}
    private readonly WalWriter writer;
    private readonly IWalDevice device;
    private Task publication=Task.CompletedTask;
    private long nextId;
    private long reserved;
    private long published;
    private Exception? failure;
    public readonly Mode Mode;
    public readonly string Root;
    public readonly Table[] Tables;
    public readonly PlainTable[] PlainTables;
    public Action<Phase>? Inject;
    public bool DisableValidation;
    public long Published=>Volatile.Read(ref this.published);
    public long Tail=>this.Tables.Sum(static t=>t.Tail)+this.PlainTables.Sum(static t=>t.Data.Store.Log.TailAddress);
    public long WalBytes{get{var path=Path.Combine(this.Root,"transactions.wal");if(File.Exists(path)){return new FileInfo(path).Length;}return 0;}}
    public void Evict(){foreach(var table in this.Tables){table.Evict();}foreach(var table in this.PlainTables){table.Data.Store.Log.FlushAndEvict(true);}}
    public Engine(Mode mode,string root,bool disk,int rows,IWalDevice? device=null,long budget=24L<<20){
        this.Mode=mode;this.Root=root;Directory.CreateDirectory(root);
        if(mode==Mode.Single){this.Tables=[];this.PlainTables=[new(Path.Combine(root,"primary"),disk,rows),new(Path.Combine(root,"secondary"),disk,64)];}
        else{this.PlainTables=[];this.Tables=[new(Path.Combine(root,"primary"),disk,rows,budget),new(Path.Combine(root,"secondary"),disk,64,budget)];}
        this.device=device??new FileWal(Path.Combine(root,"transactions.wal"));
        this.writer=new(this.device,new WalOptions{MaxBatch=64});
    }
    public Tx Begin(){
        if(this.Mode==Mode.Single){this.single.Wait();}
        try{
            lock(this.gate){this.Check();var id=Interlocked.Increment(ref this.nextId);var state=new State();this.states[id]=state;var tx=new Tx(this,id,state,this.Published);this.active.Add(tx);return tx;}
        }catch{if(this.Mode==Mode.Single){this.single.Release();}throw;}
    }
    public void Check(){if(this.failure is not null){throw new IOException("Engine stopped after commit failure",this.failure);}this.writer.Failure.ThrowIfFailed();}
    public long Csn(long owner){if(owner==0){return 0;}if(!this.states.TryGetValue(owner,out var state)){throw new InvalidDataException("Missing transaction state");}return Volatile.Read(ref state.Csn);}
    public long LatestCommitted(Key key){this.committed.TryGetValue(key,out var csn);return csn;}
    public Access[] Accesses(){
        var client=this.clients.Value!;
        if(client.Generation!=this.generation){foreach(var access in client.Accesses){access.Dispose();}client.Accesses=this.Tables.Select(static table=>new Access(table)).ToArray();client.Generation=this.generation;}
        return client.Accesses;
    }
    internal bool Commit(Tx tx){
        if(tx.Writes.Count==0){return true;}
        long order;WalReceipt receipt;Task previous;TaskCompletionSource completion;
        lock(this.gate){
            this.Check();
            if(this.Mode!=Mode.Single){foreach(var key in tx.Writes.Keys){this.Tables[key.Table].Reserve();}}
            if(!this.DisableValidation){
                foreach(var key in tx.Reads.Concat(tx.Writes.Keys)){if(this.reservations.GetValueOrDefault(key)>tx.ReadVersion){return false;}}
                foreach(var table in tx.Scans){if(this.epochs[table]>tx.ReadVersion){return false;}}
            }
            this.Inject?.Invoke(Phase.BeforeWal);
            order=++this.reserved;
            foreach(var key in tx.Writes.Keys){this.reservations[key]=order;this.epochs[key.Table]=order;}
            previous=this.publication;completion=new(TaskCreationOptions.RunContinuationsAsynchronously);this.publication=completion.Task;
            var entries=tx.Writes.Select(pair=>new WalEntry(0,pair.Key.Table+1,WalMutation.Upsert,"probe-row-v1",BitConverter.GetBytes(pair.Key.Id),Encode(pair.Value))).ToArray();
            try{receipt=this.writer.AppendAsync(new(tx.State.Id,[0],entries,BitConverter.GetBytes(order))).AsTask().GetAwaiter().GetResult();}
            catch(Exception error){this.failure=error;completion.TrySetException(error);throw new CommitFailureException(false,error);}
        }
        var durable=false;
        try{
            receipt.Durable.GetAwaiter().GetResult();durable=true;this.Inject?.Invoke(Phase.WalDurable);
            if(this.Mode==Mode.Deferred){
                foreach(var group in tx.Writes.GroupBy(static p=>p.Key.Table)){
                    foreach(var pair in group){if(this.Mode==Mode.Single){this.PlainTables[pair.Key.Table].Put(pair.Value);}else{tx.Accesses[pair.Key.Table].Put(pair.Value);}}
                    if(group.Key==0){this.Inject?.Invoke(Phase.FirstTableApplied);}
                }
            }
            previous.GetAwaiter().GetResult();
            lock(this.gate){
                this.Check();this.Inject?.Invoke(Phase.BeforePublish);
                foreach(var key in tx.Writes.Keys){this.committed[key]=order;}
                Volatile.Write(ref tx.State.Csn,order);Volatile.Write(ref this.published,order);
                completion.TrySetResult();
            }
            return true;
        }catch(Exception error){lock(this.gate){this.failure=error;completion.TrySetException(error);}throw new CommitFailureException(durable,error);}
    }
    public static byte[] Encode(Row row)=>Auto<Row>.Serialize(ref row).ToArray();
    internal void End(Tx tx){
        if(this.Mode==Mode.Single&&tx.State.Csn<=0){foreach(var pair in tx.Undo){this.PlainTables[pair.Key.Table].Put(pair.Value);}}
        lock(this.gate){this.active.Remove(tx);if(tx.State.Csn==0){Volatile.Write(ref tx.State.Csn,-1);}}
        if(tx.Writes.Count==0||this.Mode!=Mode.Immediate&&tx.State.Csn<0){this.states.TryRemove(tx.Id,out _);}
        if(this.Mode==Mode.Single){this.single.Release();}
    }
    public long Collect(){
        lock(this.gate){
            this.Check();if(this.active.Count!=0){throw new InvalidOperationException("Active snapshots retain history");}
            this.publication.GetAwaiter().GetResult();
            // native Compact は物理的な最新値だけを残す。中止版を残して旧確定版を落とさない。
            foreach(var access in this.Accesses()){
                foreach(var row in access.Rows){if(row.Owner!=0&&this.Csn(row.Owner)<=0){throw new InvalidOperationException("Aborted latest versions require MVCC-aware compaction");}}
            }
            // 保存先の Compact は旧版のリンクを保持しない。採用試験では静止時だけ実行する。
            foreach(var client in this.clients.Values){foreach(var access in client.Accesses){access.Dispose();}}this.generation++;
            long reclaimed=0;foreach(var table in this.Tables){reclaimed+=table.Compact();}
            return reclaimed;
        }
    }
    public void Dispose(){
        this.writer.Dispose();this.device.Dispose();foreach(var client in this.clients.Values){foreach(var access in client.Accesses){access.Dispose();}}this.clients.Dispose();foreach(var table in this.Tables){table.Dispose();}foreach(var table in this.PlainTables){table.Dispose();}this.single.Dispose();
    }
}

public sealed class Tx:IDisposable{
    private readonly Engine engine;
    private bool closed;
    public readonly long Id;
    public readonly State State;
    public readonly long ReadVersion;
    public readonly Access[] Accesses;
    public readonly Dictionary<Key,Row> Writes=[];
    public readonly Dictionary<Key,Row> Undo=[];
    public readonly HashSet<Key> Reads=[];
    public readonly HashSet<int> Scans=[];
    public Tx(Engine engine,long id,State state,long readVersion){this.engine=engine;this.Id=id;this.State=state;this.ReadVersion=readVersion;this.Accesses=engine.Accesses();}
    public Seamless.Collections.Tsavorite.Set<int,PlainRow>? PlainRows{get{if(this.engine.Mode==Mode.Single){return this.engine.PlainTables[0].Rows;}return null;}}
    public Row Read(int id,int table=0){
        var key=new Key(table,id);this.Reads.Add(key);if(this.Writes.TryGetValue(key,out var own)){return own;}
        if(this.engine.Mode==Mode.Single){if(this.engine.PlainTables[table].Rows.TryGetValue(id,out var plain)){return new(){Id=id,Group=plain.Group,Value=plain.Value};}return new(){Id=id,Deleted=1};}
        return this.Resolve(this.Accesses[table].Latest(id),table);
    }
    public Row Visible(Row candidate,int table=0){return this.Resolve(new(candidate,0,candidate.Previous,true),table);}
    private Row Resolve(ReadResult candidate,int table){
        if(!candidate.Captured){return new(){Deleted=1};}
        var key=new Key(table,candidate.Row.Id);
        if(this.Writes.TryGetValue(key,out var own)){return own;}
        var target=this.engine.LatestCommitted(key);var best=-1L;var result=new Row{Id=key.Id,Deleted=1};
        var limit=0;
        while(candidate.Captured){
            if(++limit>100_000){throw new InvalidDataException("History loop");}
            var row=candidate.Row;
            if(row.Id==key.Id){
                var csn=this.engine.Csn(row.Owner);
                if((row.Owner==0||csn>0)&&csn<=this.ReadVersion&&csn>best){best=csn;result=row;if(csn==target&&target<=this.ReadVersion){return result;}}
            }
            if(candidate.Previous<this.engine.Tables[table].Data.Store.Log.BeginAddress||candidate.Previous<=1){break;}
            candidate=this.Accesses[table].At(candidate.Previous);
        }
        return result;
    }
    public void Set(int id,long value,int table=0,int? group=null,bool deleted=false){
        this.engine.Check();var key=new Key(table,id);var row=new Row{Id=id,Group=id%64,Value=value,Owner=this.Id};
        if(this.engine.Mode==Mode.Single&&!this.Undo.ContainsKey(key)){this.Undo[key]=this.Read(id,table);}
        if(group.HasValue){row.Group=group.Value;}if(deleted){row.Deleted=1;}
        this.Writes[key]=row;
        if(this.engine.Mode==Mode.Immediate){this.Accesses[table].Put(row);}
        if(this.engine.Mode==Mode.Single){this.engine.PlainTables[table].Put(row);}
    }
    public void RegisterScan(int table=0)=>this.Scans.Add(table);
    public IDisposable Scan(int table=0){this.RegisterScan(table);if(this.engine.Mode==Mode.Single){return EmptyLease.Instance;}var gate=this.engine.Tables[table].Structure;gate.EnterReadLock();return new ScanLease(gate);}
    private sealed class ScanLease(ReaderWriterLockSlim gate):IDisposable{public void Dispose()=>gate.ExitReadLock();}
    private sealed class EmptyLease:IDisposable{public static readonly EmptyLease Instance=new();public void Dispose(){}}
    public IEnumerable<Row> Additional(int table=0){
        if(this.engine.Mode is Mode.Immediate or Mode.Single){yield break;}
        foreach(var pair in this.Writes){if(pair.Key.Table==table&&!this.Accesses[table].Latest(pair.Key.Id).Captured&&!pair.Value.Deleted.Equals(1L)){yield return pair.Value;}}
    }
    public bool Commit(){try{return this.engine.Commit(this);}finally{this.Dispose();}}
    public void Dispose(){if(this.closed){return;}this.closed=true;this.engine.End(this);}
}

public sealed class FileWal: IWalDevice{
    private readonly FileStream stream;
    public FileWal(string path){this.stream=new(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);}
    public void Write(byte[] frame)=>this.stream.Write(frame);
    public void Sync()=>this.stream.Flush(true);
    public void Dispose()=>this.stream.Dispose();
    public static IEnumerable<WalTransaction> Read(string path){
        using var stream=File.OpenRead(path);var header=new byte[WalFormat.HeaderBytes];
        while(stream.Position<stream.Length){if(stream.Length-stream.Position<header.Length){yield break;}stream.ReadExactly(header);var parsed=WalFormat.ReadHeader(header,4<<20);var frame=new byte[parsed.Length];header.CopyTo(frame,0);if(stream.Length-stream.Position<frame.Length-header.Length){yield break;}stream.ReadExactly(frame.AsSpan(header.Length));yield return WalFormat.Decode(frame);}
    }
}
