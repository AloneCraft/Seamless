using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
    public long AvailableMemoryBytes{get{lock(this.Gate){var available=long.MaxValue;for(var budget=this;budget is not null;budget=budget.Parent){available=Math.Min(available,budget.memoryLimit-budget.memory);}return available;}}}
    public long TemporaryStorageBytes{get{lock(this.Gate){return this.storage;}}}
    public long PeakMemoryBytes{get{lock(this.Gate){return this.peakMemory;}}}
    public long PeakTemporaryStorageBytes{get{lock(this.Gate){return this.peakStorage;}}}
    public ResourceReservation ReserveMemory(long bytes)=>new(this,ResourceLimitKind.Memory,bytes);
    public ResourceReservation ReserveTemporaryStorage(long bytes)=>new(this,ResourceLimitKind.TemporaryStorage,bytes);
    // 共同枠は保持量に対する追加制約。祖先のメモリ使用量には実予約だけを加える。
    internal MemoryPool CreateMemoryPool(long limit)=>new(this.Gate,limit);
    internal ResourceReservation ReserveMemory(long bytes,MemoryPool pool)=>new(this,ResourceLimitKind.Memory,bytes,pool);
    internal sealed class MemoryPool(object gate,long limit){
        internal readonly object Gate=gate;
        internal readonly long Limit=limit;
        internal long Used;
    }
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
    private readonly ResourceBudget.MemoryPool? pool;
    private long bytes;
    private bool disposed;
    internal ResourceReservation(ResourceBudget budget,ResourceLimitKind kind,long bytes,ResourceBudget.MemoryPool? pool=null){
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        if(pool is not null&&(!ReferenceEquals(pool.Gate,budget.Gate)||kind!=ResourceLimitKind.Memory)){
            throw new ArgumentException("The memory pool must share the reservation's budget gate.",nameof(pool));
        }
        this.budget=budget;this.kind=kind;
        this.pool=pool;
        lock(this.budget.Gate){
            this.AdjustUnderLock(bytes);
            this.bytes=bytes;
        }
    }
    public long Bytes=>Volatile.Read(ref this.bytes);
    public void Resize(long bytes){
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock(this.budget.Gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            this.AdjustUnderLock(checked(bytes-this.bytes));
            this.bytes=bytes;
        }
    }
    internal bool TryResize(long bytes){
        try{this.Resize(bytes);return true;}
        catch(ResourceLimitException){return false;}
    }
    private void AdjustUnderLock(long delta){
        if(this.pool is{} pool&&delta>pool.Limit-pool.Used){throw new ResourceLimitException(this.kind,delta,pool.Limit);}
        this.budget.AdjustUnderLock(this.kind,delta);
        if(this.pool is not null){this.pool.Used+=delta;}
    }
    public void Dispose(){
        lock(this.budget.Gate){
            if(this.disposed){return;}
            this.AdjustUnderLock(-this.bytes);
            this.bytes=0;this.disposed=true;
        }
    }
}
