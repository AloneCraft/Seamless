namespace Seamless.Execution;
public sealed class ResourceBudget{
    private readonly object gate=new();
    private readonly long memoryLimit;
    private readonly long storageLimit;
    private long memory;
    private long storage;
    private long peakMemory;
    private long peakStorage;
    public ResourceBudget(long memoryLimitBytes,long temporaryStorageLimitBytes){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryLimitBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(temporaryStorageLimitBytes);
        this.memoryLimit=memoryLimitBytes;
        this.storageLimit=temporaryStorageLimitBytes;
    }
    public long MemoryLimitBytes=>this.memoryLimit;
    public long TemporaryStorageLimitBytes=>this.storageLimit;
    public long MemoryBytes{get{lock(this.gate){return this.memory;}}}
    public long TemporaryStorageBytes{get{lock(this.gate){return this.storage;}}}
    public long PeakMemoryBytes{get{lock(this.gate){return this.peakMemory;}}}
    public long PeakTemporaryStorageBytes{get{lock(this.gate){return this.peakStorage;}}}
    public ResourceReservation ReserveMemory(long bytes)=>new(this,ResourceLimitKind.Memory,bytes);
    public ResourceReservation ReserveTemporaryStorage(long bytes)=>new(this,ResourceLimitKind.TemporaryStorage,bytes);
    internal void Adjust(ResourceLimitKind kind,long delta){
        lock(this.gate){
            long current,limit;
            if(kind==ResourceLimitKind.Memory){current=this.memory;limit=this.memoryLimit;}
            else{current=this.storage;limit=this.storageLimit;}
            if(delta>limit-current){throw new ResourceLimitException(kind,delta,limit);}
            var next=checked(current+delta);
            if(next<0){throw new InvalidOperationException("A resource reservation was released more than once.");}
            if(kind==ResourceLimitKind.Memory){this.memory=next;this.peakMemory=Math.Max(this.peakMemory,next);}
            else{this.storage=next;this.peakStorage=Math.Max(this.peakStorage,next);}
        }
    }
}
public sealed class ResourceReservation:IDisposable{
    private readonly ResourceBudget budget;
    private readonly ResourceLimitKind kind;
    private long bytes;
    private bool disposed;
    internal ResourceReservation(ResourceBudget budget,ResourceLimitKind kind,long bytes){
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        this.budget=budget;this.kind=kind;
        this.budget.Adjust(kind,bytes);
        this.bytes=bytes;
    }
    public long Bytes=>this.bytes;
    public void Resize(long bytes){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        this.budget.Adjust(this.kind,checked(bytes-this.bytes));
        this.bytes=bytes;
    }
    public void Dispose(){
        if(this.disposed){return;}
        this.budget.Adjust(this.kind,-this.bytes);
        this.bytes=0;this.disposed=true;
    }
}
