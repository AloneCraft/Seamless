namespace Seamless.Execution;
public sealed class ResourceBudget{
    internal object Gate{get;}
    public ResourceBudget? Parent{get;}
    private readonly long memoryLimit;
    private readonly long storageLimit;
    private long memory;
    private long storage;
    private long peakMemory;
    private long peakStorage;
    public ResourceBudget(long memoryLimitBytes,long temporaryStorageLimitBytes):this(memoryLimitBytes,temporaryStorageLimitBytes,null){}
    private ResourceBudget(long memoryLimitBytes,long temporaryStorageLimitBytes,ResourceBudget? parent){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryLimitBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(temporaryStorageLimitBytes);
        this.memoryLimit=memoryLimitBytes;
        this.storageLimit=temporaryStorageLimitBytes;
        this.Parent=parent;
        this.Gate=parent?.Gate??new object();
    }
    public ResourceBudget CreateChild(long memoryLimitBytes,long temporaryStorageLimitBytes)=>new(memoryLimitBytes,temporaryStorageLimitBytes,this);
    public long MemoryLimitBytes=>this.memoryLimit;
    public long TemporaryStorageLimitBytes=>this.storageLimit;
    public long MemoryBytes{get{lock(this.Gate){return this.memory;}}}
    public long TemporaryStorageBytes{get{lock(this.Gate){return this.storage;}}}
    public long PeakMemoryBytes{get{lock(this.Gate){return this.peakMemory;}}}
    public long PeakTemporaryStorageBytes{get{lock(this.Gate){return this.peakStorage;}}}
    public ResourceReservation ReserveMemory(long bytes)=>new(this,ResourceLimitKind.Memory,bytes);
    public ResourceReservation ReserveTemporaryStorage(long bytes)=>new(this,ResourceLimitKind.TemporaryStorage,bytes);
    internal void AdjustUnderLock(ResourceLimitKind kind,long delta){
        // 階層全体で同じロックを共有し、全祖先の検証後に会計を確定する。
        System.Diagnostics.Debug.Assert(Monitor.IsEntered(this.Gate));
        for(var budget=this;budget is not null;budget=budget.Parent){
            long current=budget.storage,limit=budget.storageLimit;
            if(kind==ResourceLimitKind.Memory){current=budget.memory;limit=budget.memoryLimit;}
            if(delta>limit-current){throw new ResourceLimitException(kind,delta,limit);}
            if(checked(current+delta)<0){throw new InvalidOperationException("A resource reservation was released more than once.");}
        }
        for(var budget=this;budget is not null;budget=budget.Parent){
            if(kind==ResourceLimitKind.Memory){budget.memory+=delta;budget.peakMemory=Math.Max(budget.peakMemory,budget.memory);}
            else{budget.storage+=delta;budget.peakStorage=Math.Max(budget.peakStorage,budget.storage);}
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
        lock(this.budget.Gate){
            this.budget.AdjustUnderLock(kind,bytes);
            this.bytes=bytes;
        }
    }
    public long Bytes=>Volatile.Read(ref this.bytes);
    public void Resize(long bytes){
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock(this.budget.Gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            this.budget.AdjustUnderLock(this.kind,checked(bytes-this.bytes));
            this.bytes=bytes;
        }
    }
    public void Dispose(){
        lock(this.budget.Gate){
            if(this.disposed){return;}
            this.budget.AdjustUnderLock(this.kind,-this.bytes);
            this.bytes=0;this.disposed=true;
        }
    }
}
