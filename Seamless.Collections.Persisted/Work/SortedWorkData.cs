using System.Buffers.Binary;
using System.Text.Json;
using Seamless.Execution;
using CommonStore=Seamless.Execution.Store;
namespace Seamless.Collections.Persisted.Work;

/// <summary>入力を変更せず、予算内の位置索引を外部ソートする。完成索引だけを公開する。</summary>
internal sealed class SortedWorkData<T,TKey>:IStoreComponent,IPersistedStorageQuota{
    private readonly CommonStore owner;
    private readonly string directory;
    private readonly string kind;
    private readonly string comparisonId;
    private readonly Func<T,TKey> key;
    private readonly IComparer<TKey> comparer;
    private readonly bool distinct;
    private readonly long sortMemoryBytes;
    private readonly StorageTuning tuning;
    private readonly WorkSnapshotFiles snapshots;
    private SerializedList<T,AutoSerializer<T>> records=null!;
    private ResourceReservation dataDisk=null!;
    private PositionFile index=null!;
    private string working=string.Empty;
    private bool ready;
    private bool disposed;
    private long count;
    private long version;
    public string Name{get;}

    internal SortedWorkData(CommonStore owner,string name,string kind,Func<T,TKey> key,IComparer<TKey>? comparer,string? comparisonId,bool distinct,long? sortMemoryBytes,StorageTuning? tuning){
        ArgumentNullException.ThrowIfNull(owner);
        if(owner.Kind==StoreKind.Memory){throw new NotSupportedException("Persisted work collections require a file-backed Store.");}
        this.owner=owner;this.Name=name;this.kind=kind;this.key=key;this.distinct=distinct;
        if(comparer is null){
            if(typeof(TKey)==typeof(string)){this.comparer=(IComparer<TKey>)(object)StringComparer.Ordinal;}
            else{this.comparer=Comparer<TKey>.Default;}
            this.comparisonId="default-ordinal-v1:"+typeof(TKey).AssemblyQualifiedName;
        }else{
            if(string.IsNullOrWhiteSpace(comparisonId)&&owner.Kind==StoreKind.Persistent){throw new ArgumentException("A persistent custom comparer requires a stable comparisonId.",nameof(comparisonId));}
            this.comparer=comparer;
            this.comparisonId=comparer.GetType().AssemblyQualifiedName+"|"+comparisonId;
        }
        this.sortMemoryBytes=sortMemoryBytes??Math.Min(65536,Math.Max(64,owner.Limits.MemoryBytes/4));
        if(this.sortMemoryBytes<64){throw new ArgumentOutOfRangeException(nameof(sortMemoryBytes));}
        this.tuning=tuning??StorageTuning.Default;
        this.directory=owner.GetComponentDirectory(name);
        if(owner.TryGetSavedState(name,out var saved)){this.Parse(saved);}
        this.snapshots=new(owner.Budget,this.directory);
        try{
            this.working=this.NewWorkingDirectory();
            this.dataDisk=owner.Budget.ReserveTemporaryStorage(0);
            this.records=new(Path.Combine(this.working,"records.dat"),1,1,this.tuning,null,this);
            this.EnsureTotalBytes(((IPersistedCommittable)this.records).StorageBytes);
            ((IPersistedCommittable)this.records).StorageQuota=this;
            this.index=new(owner.Budget,Path.Combine(this.working,"empty.idx"));
            owner.Register(this);
        }catch{this.Dispose();throw;}
    }
    internal bool IsReady{get{this.Check();return this.ready;}}
    internal long AddedCount{get{this.Check();return this.records.Count;}}
    internal long Count{get{this.Check();if(this.ready){return this.count;}return this.records.Count;}}
    internal void Add(T value){
        this.Check();
        var encoded=AutoSerializer<T>.Serialize(ref value);
        using var memory=this.owner.Budget.ReserveMemory(checked(128+4L*encoded.Length));
        this.ready=false;this.version++;
        this.records.Add(value);
    }
    internal T At(long ordinal){
        this.Check();
        if((ulong)ordinal>=(ulong)this.Count){throw new ArgumentOutOfRangeException(nameof(ordinal));}
        long position=ordinal;
        if(this.ready){position=this.index.Read(ordinal);}
        using var memory=this.ReadMemory(position);
        return this.records[position];
    }
    internal long Find(TKey key){
        this.RequireReady();
        var lower=this.LowerBound(key);
        if(lower<this.count&&this.CompareKey(this.index.Read(lower),key)==0){return lower;}
        return -1;
    }
    internal IEnumerable<T> Enumerate(){
        this.Check();
        return this.EnumerateCore(0,this.Count,this.version);
    }
    internal IEnumerable<T> Range(TKey lower,TKey upper){
        this.RequireReady();
        if(this.comparer.Compare(lower,upper)>0){throw new ArgumentException("The lower bound exceeds the upper bound.");}
        return this.EnumerateCore(this.LowerBound(lower),this.LowerBound(upper),this.version);
    }
    private IEnumerable<T> EnumerateCore(long start,long end,long expectedVersion){
        for(var position=start;position<end;position++){
            this.Check();
            if(this.version!=expectedVersion){throw new InvalidOperationException("The work collection changed during enumeration.");}
            yield return this.At(position);
        }
        this.Check();
        if(this.version!=expectedVersion){throw new InvalidOperationException("The work collection changed during enumeration.");}
    }
    private long LowerBound(TKey key){
        long low=0,high=this.count;
        while(low<high){
            var middle=low+(high-low)/2;
            if(this.CompareKey(this.index.Read(middle),key)<0){low=middle+1;}else{high=middle;}
        }
        return low;
    }
    private ResourceReservation ReadMemory(long first,long second=-1){
        long bytes=this.records.GetSerializedByteLength(first);
        if(second>=0){bytes=checked(bytes+this.records.GetSerializedByteLength(second));}
        return this.owner.Budget.ReserveMemory(checked(256+4*bytes));
    }
    private int CompareKey(long position,TKey key){
        using var memory=this.ReadMemory(position);
        return this.comparer.Compare(this.key(this.records[position]),key);
    }
    private int CompareKeys(long first,long second){
        using var memory=this.ReadMemory(first,second);
        return this.comparer.Compare(this.key(this.records[first]),this.key(this.records[second]));
    }
    private int ComparePositions(long first,long second){
        var comparison=this.CompareKeys(first,second);
        if(comparison!=0){return comparison;}
        return first.CompareTo(second);
    }
    internal void CompleteAdding(){
        this.Check();
        if(this.ready){return;}
        using var metadata=this.owner.Budget.ReserveMemory(1024);
        var capacity=(int)Math.Min(1048576,Math.Max(1,(this.sortMemoryBytes-32)/sizeof(long)));
        using var rowMemory=this.owner.Budget.ReserveMemory(checked(32L+capacity*sizeof(long)));
        var rows=new long[capacity];
        var runs=new PositionFile?[64];
        PositionFile? merged=null;
        PositionFile? candidate=null;
        var comparison=Comparer<long>.Create(this.ComparePositions);
        try{
            long position=0;
            while(position<this.records.Count){
                var length=(int)Math.Min(capacity,this.records.Count-position);
                for(var item=0;item<length;item++){rows[item]=position+item;}
                Array.Sort(rows,0,length,comparison);
                var run=this.NewIndex("run");
                try{for(var item=0;item<length;item++){run.Append(rows[item]);}}
                catch{run.Dispose();throw;}
                this.AddRun(run,runs);
                position+=length;
            }
            for(var level=0;level<runs.Length;level++){
                var run=runs[level];runs[level]=null;
                if(run is null){continue;}
                if(merged is null){merged=run;}else{merged=this.Merge(merged,run);}
            }
            candidate=this.NewIndex("sorted");
            if(merged is not null){
                if(!this.distinct){
                    for(long item=0;item<merged.Count;item++){candidate.Append(merged.Read(item));}
                }else{
                    long pending=-1;
                    for(long item=0;item<merged.Count;item++){
                        var current=merged.Read(item);
                        if(pending>=0&&this.CompareKeys(pending,current)!=0){candidate.Append(pending);}
                        // 同値キーは入力位置の大きいものを採用し、安定ソートに依存しない。
                        pending=current;
                    }
                    if(pending>=0){candidate.Append(pending);}
                }
            }
            candidate.Flush();
            // 後始末も成功してから Ready にする。失敗時は入力だけが引き続き有効。
            merged?.Dispose();merged=null;
            DisposeRuns(runs);
            Array.Clear(runs);
            this.index.Dispose();
            this.index=candidate;candidate=null;
            this.count=this.index.Count;this.ready=true;this.version++;
        }finally{
            try{candidate?.Dispose();}finally{try{merged?.Dispose();}finally{DisposeRuns(runs);}}
        }
    }
    private void AddRun(PositionFile run,PositionFile?[] runs){
        try{
            for(var level=0;level<runs.Length;level++){
                if(runs[level] is null){runs[level]=run;return;}
                var previous=runs[level]!;runs[level]=null;
                run=this.Merge(previous,run);
            }
            throw new OverflowException("Too many sort runs.");
        }catch{run.Dispose();throw;}
    }
    private PositionFile Merge(PositionFile first,PositionFile second){
        PositionFile? result=null;
        try{
            result=this.NewIndex("merge");
            long left=0,right=0;
            while(left<first.Count||right<second.Count){
                if(left<first.Count&&(right>=second.Count||this.ComparePositions(first.Read(left),second.Read(right))<=0)){
                    result.Append(first.Read(left++));
                }else{result.Append(second.Read(right++));}
            }
            first.Dispose();second.Dispose();
            var completed=result;result=null;
            return completed;
        }finally{
            try{result?.Dispose();}finally{try{first.Dispose();}finally{second.Dispose();}}
        }
    }
    private static void DisposeRuns(PositionFile?[] runs){
        Exception? error=null;
        foreach(var run in runs){
            try{run?.Dispose();}catch(Exception failure){error??=failure;}
        }
        if(error is not null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();}
    }
    private PositionFile NewIndex(string prefix)=>new(this.owner.Budget,Path.Combine(this.working,prefix+"-"+Guid.NewGuid().ToString("N")+".idx"));
    private string NewWorkingDirectory(){
        var path=Path.Combine(this.directory,"active-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);return path;
    }
    public void EnsureTotalBytes(long bytes)=>this.dataDisk.Resize(bytes);
    public string Save(){
        this.Check();
        var state=((IPersistedCommittable)this.records).Commit();
        this.index.Flush();
        var snapshot=this.snapshots.Capture([this.records.Path,this.records.IndexPath,this.index.Path]);
        return JsonSerializer.Serialize(new SavedState("sorted-work-v1",this.kind,typeof(T).AssemblyQualifiedName!,this.comparisonId,snapshot,state,this.ready,this.count));
    }
    public void Load(string state){
        this.Check();
        var saved=this.Parse(state);
        var paths=this.snapshots.GetPaths(saved.Snapshot,3);
        var directory=this.NewWorkingDirectory();
        ResourceReservation? disk=null;
        SerializedList<T,AutoSerializer<T>>? records=null;
        PositionFile? index=null;
        try{
            disk=this.owner.Budget.ReserveTemporaryStorage(checked(new FileInfo(paths[0]).Length+new FileInfo(paths[1]).Length));
            using(var memory=this.owner.Budget.ReserveMemory(4096)){
                var buffer=new byte[4096];
                WorkSnapshotFiles.Copy(paths[0],Path.Combine(directory,"records.dat"),buffer);
                WorkSnapshotFiles.Copy(paths[1],Path.Combine(directory,"records.dat.index"),buffer);
            }
            records=new(Path.Combine(directory,"records.dat"),1,1,this.tuning);
            if(records.Count!=saved.Data.Count||records.DataLength!=saved.Data.DataLength){throw new InvalidDataException("The input snapshot does not match its saved state.");}
            index=new(this.owner.Budget,Path.Combine(directory,"restored.idx"));
            using(var input=new FileStream(paths[2],FileMode.Open,FileAccess.Read,FileShare.Read,1)){
                if(input.Length%8!=0){throw new InvalidDataException("The sorted index length is invalid.");}
                Span<byte> bytes=stackalloc byte[8];
                while(input.Position<input.Length){
                    input.ReadExactly(bytes);var position=BinaryPrimitives.ReadInt64LittleEndian(bytes);
                    if(position<0||position>=records.Count){throw new InvalidDataException("The sorted index references an invalid input row.");}
                    index.Append(position);
                }
            }
            if(saved.Ready&&index.Count!=saved.Count){throw new InvalidDataException("The sorted index count is invalid.");}
            var previousRecords=this.records;var previousDisk=this.dataDisk;var previousIndex=this.index;var previousDirectory=this.working;
            this.records=records;records=null;this.dataDisk=disk;disk=null;this.index=index;index=null;this.working=directory;
            ((IPersistedCommittable)this.records).StorageQuota=this;
            this.ready=saved.Ready;this.count=saved.Count;this.version++;
            try{previousRecords.Dispose();}finally{try{previousIndex.Dispose();}finally{previousDisk.Dispose();Directory.Delete(previousDirectory,true);}}
        }catch{
            try{records?.Dispose();}finally{
                try{index?.Dispose();}finally{
                    try{disk?.Dispose();}finally{if(this.working!=directory&&Directory.Exists(directory)){Directory.Delete(directory,true);}}
                }
            }
            throw;
        }
    }
    private SavedState Parse(string text){
        SavedState? saved;
        try{saved=JsonSerializer.Deserialize<SavedState>(text);}
        catch(JsonException error){throw new InvalidDataException("Not a sorted work collection state.",error);}
        if(saved is null||saved.Format!="sorted-work-v1"||saved.Kind!=this.kind||saved.ElementType!=typeof(T).AssemblyQualifiedName||saved.ComparisonId!=this.comparisonId){
            throw new InvalidDataException("The saved collection type or comparison rule differs from the requested collection.");
        }
        if(saved.Count<0){throw new InvalidDataException("The saved count is invalid.");}
        return saved;
    }
    private void Check()=>ObjectDisposedException.ThrowIf(this.disposed,this);
    private void RequireReady(){this.Check();if(!this.ready){throw new InvalidOperationException("Call CompleteAdding successfully before searching the collection.");}}
    public void Dispose(){
        if(this.disposed){return;}this.disposed=true;
        try{this.records?.Dispose();}
        finally{
            try{this.index?.Dispose();}
            finally{
                this.dataDisk?.Dispose();this.snapshots.Dispose();this.owner.Unregister(this);
                if(this.working.Length>0&&Directory.Exists(this.working)){Directory.Delete(this.working,true);}
            }
        }
    }
    private sealed record SavedState(string Format,string Kind,string ElementType,string ComparisonId,string Snapshot,PersistedCommitState Data,bool Ready,long Count);
}

internal sealed class PositionFile:IDisposable{
    private readonly FileStream stream;
    private readonly ResourceReservation disk;
    private bool disposed;
    internal string Path{get;}
    internal long Count=>this.stream.Length/8;
    internal PositionFile(ResourceBudget budget,string path){
        this.Path=path;this.disk=budget.ReserveTemporaryStorage(0);
        try{this.stream=new(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read,1);}
        catch{this.disk.Dispose();throw;}
    }
    internal void Append(long position){
        var length=checked(this.stream.Length+8);this.disk.Resize(length);
        try{
            this.stream.Position=this.stream.Length;
            Span<byte> bytes=stackalloc byte[8];BinaryPrimitives.WriteInt64LittleEndian(bytes,position);this.stream.Write(bytes);
        }catch{this.disk.Resize(this.stream.Length);throw;}
    }
    internal long Read(long index){
        if((ulong)index>=(ulong)this.Count){throw new ArgumentOutOfRangeException(nameof(index));}
        this.stream.Position=checked(index*8);
        Span<byte> bytes=stackalloc byte[8];this.stream.ReadExactly(bytes);return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }
    internal void Flush()=>this.stream.Flush(true);
    public void Dispose(){
        if(this.disposed){return;}this.disposed=true;
        try{this.stream.Dispose();}finally{try{File.Delete(this.Path);}finally{this.disk.Dispose();}}
    }
}
