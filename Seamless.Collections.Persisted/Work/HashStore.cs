using System.Text.Json;
using Seamless.Execution;
using CommonStore=Seamless.Execution.Store;
namespace Seamless.Collections.Persisted.Work;

internal abstract class HashRecords<T,TSerializer>:SerializedList<T,TSerializer> where TSerializer:ISerializer<T>{
    protected HashRecords(string path,StorageTuning? tuning,IPersistedKeyPolicy policy,IPersistedStorageQuota quota)
        :base(path,16,4096,tuning,policy,quota){
    }
    protected override bool UsesKeyIndex=>true;
    internal long DistinctCount=>this.KeyIndexEntryCount;
    internal bool Lookup(ReadOnlySpan<byte> key,out long offset)=>this.TryLookupKey(key,out offset);
    internal int ByteLengthAt(long offset)=>this.GetRecordByteLength(offset);
    internal T ReadAt(long offset)=>TSerializer.Deserialize(this.ReadRecordPayload(offset));
    internal bool IsLatest(long index){
        var offset=this.GetRecordOffset(index);
        var payload=this.ReadRecordPayload(offset);
        return this.TryLookupKey(this.ExtractKeyBytes(payload),out var latest)&&latest==offset;
    }
}

internal sealed class HashKeyPolicy<TKey,TSerializer>(IEqualityComparer<TKey> comparer,ResourceBudget budget):IPersistedKeyPolicy where TSerializer:ISerializer<TKey>{
    public ulong Hash(ReadOnlySpan<byte> key){
        var value=TSerializer.Deserialize(key);
        if(value is null){
            return 0;
        }
        return unchecked((uint)comparer.GetHashCode(value));
    }
    public bool Equals(ReadOnlySpan<byte> left,ReadOnlySpan<byte> right)=>comparer.Equals(TSerializer.Deserialize(left),TSerializer.Deserialize(right));
    public IDisposable ReserveRead(long bytes)=>budget.ReserveMemory(bytes);
}

/// <summary>hash 作業コレクションの保存・寿命・予算。各保存の生ファイルを不変な世代へ写す。</summary>
internal sealed class HashStore<T,TSerializer>:IStoreComponent where TSerializer:ISerializer<T>{
    private const string Format="seamless-work-hash-v1";
    private readonly CommonStore owner;
    private readonly string directory;
    private readonly string kind;
    private readonly string comparerId;
    private readonly Func<string,IPersistedStorageQuota,HashRecords<T,TSerializer>> create;
    private readonly WorkSnapshotFiles snapshots;
    private Generation current=null!;
    private bool disposed;
    internal HashStore(CommonStore owner,string name,string kind,string comparerId,Func<string,IPersistedStorageQuota,HashRecords<T,TSerializer>> create){
        this.owner=owner;
        this.Name=name;
        this.kind=kind;
        this.comparerId=comparerId;
        this.create=create;
        if(owner.Kind==StoreKind.Memory){
            throw new NotSupportedException("Persisted work collections require a file-backed store.");
        }
        if(owner.TryGetSavedState(name,out var saved)){
            this.Validate(saved);
        }
        this.directory=owner.GetComponentDirectory(name);
        this.snapshots=new(owner.Budget,this.directory);
        try{
            this.current=this.CreateGeneration(null);
            owner.Register(this);
        }catch{
            this.current?.Dispose();
            this.snapshots.Dispose();
            throw;
        }
    }
    public string Name{get;}
    internal ResourceBudget Budget=>this.owner.Budget;
    internal bool IsDisposed=>this.disposed;
    internal HashRecords<T,TSerializer> Records{
        get{
            ObjectDisposedException.ThrowIf(this.disposed,this);
            return this.current.Records;
        }
    }
    public string Save(){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var state=((IPersistedCommittable)this.Records).Commit();
        var snapshot=this.snapshots.Capture([this.Records.Path,this.Records.IndexPath]);
        return JsonSerializer.Serialize(new Saved(Format,this.kind,this.comparerId,snapshot,state));
    }
    // Save(Commit とスナップショットの書き出し)が既にディスクへ確実に書き出しているので、ここですることは無い。
    public void Flush(){}
    public void Load(string state){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var saved=this.Validate(state);
        var paths=this.snapshots.GetPaths(saved.Snapshot,2);
        var replacement=this.CreateGeneration(paths);
        try{
            ((IPersistedCommittable)replacement.Records).RollbackTo(saved.State);
        }catch{
            replacement.Dispose();
            throw;
        }
        var previous=this.current;
        this.current=replacement;
        previous.Dispose();
    }
    private Saved Validate(string state){
        var saved=JsonSerializer.Deserialize<Saved>(state);
        if(saved is null||saved.Format!=Format||saved.Kind!=this.kind||saved.ComparerId!=this.comparerId){
            throw new InvalidDataException("The saved work collection format, kind, or comparer does not match.");
        }
        return saved;
    }
    private Generation CreateGeneration(string[]? source){
        var generation=new Generation(this.owner.Budget,Path.Combine(this.directory,"working-"+Guid.NewGuid().ToString("N")));
        try{
            Directory.CreateDirectory(generation.Directory);
            var path=Path.Combine(generation.Directory,"items.dat");
            if(source is not null){
                var length=checked(new FileInfo(source[0]).Length+new FileInfo(source[1]).Length);
                generation.EnsureTotalBytes(length);
                using var memory=this.owner.Budget.ReserveMemory(4096);
                var buffer=new byte[4096];
                WorkSnapshotFiles.Copy(source[0],path,buffer);
                WorkSnapshotFiles.Copy(source[1],path+".index",buffer);
            }
            generation.Records=this.create(path,generation);
            generation.EnsureTotalBytes(((IPersistedCommittable)generation.Records).StorageBytes);
            return generation;
        }catch{
            generation.Dispose();
            throw;
        }
    }
    internal IEnumerable<T> Enumerate(bool latestOnly){
        var records=this.Records;
        var count=records.Count;
        for(long index=0;index<count;index++){
            this.CheckEnumeration(records,count);
            using var memory=this.Budget.ReserveMemory(checked(128+3L*records.GetSerializedByteLength(index)));
            if(!latestOnly||records.IsLatest(index)){
                yield return records[index];
            }
        }
        this.CheckEnumeration(records,count);
    }
    private void CheckEnumeration(HashRecords<T,TSerializer> records,long count){
        if(!ReferenceEquals(records,this.Records)||records.Count!=count){
            throw new InvalidOperationException("The work collection changed during enumeration.");
        }
    }
    public void Dispose(){
        if(this.disposed){
            return;
        }
        this.disposed=true;
        try{
            this.current.Dispose();
        }finally{
            this.snapshots.Dispose();
            this.owner.Unregister(this);
        }
    }
    private sealed record Saved(string Format,string Kind,string ComparerId,string Snapshot,PersistedCommitState State);
    private sealed class Generation(ResourceBudget budget,string directory):IPersistedStorageQuota,IDisposable{
        private readonly ResourceReservation disk=budget.ReserveTemporaryStorage(0);
        internal string Directory=>directory;
        internal HashRecords<T,TSerializer> Records{get;set;}=null!;
        public void EnsureTotalBytes(long totalBytes)=>this.disk.Resize(totalBytes);
        public void Dispose(){
            try{
                this.Records?.Dispose();
            }finally{
                try{
                    if(System.IO.Directory.Exists(directory)){
                        System.IO.Directory.Delete(directory,true);
                    }
                }finally{
                    this.disk.Dispose();
                }
            }
        }
    }
}
