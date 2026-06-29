using Seamless.Tsavorite.Devices;
namespace Seamless.Tsavorite.Stores;

public abstract class CollectionStore{
    /// <summary>CollectionStoreの境界条件を検証する。</summary>
    [Conditional("DEBUG")]
    private protected static void AssertIsPowerOfTwo(long value,string paramName){
        Debug.Assert(value>0,$"{paramName} must be positive");
        Debug.Assert((value&(value-1))==0,$"{paramName} must be a power of 2, but was {value}");
    }

    private protected static void ValidatePowerOfTwo(long value,string paramName){
        if(value<=0)
            throw new ArgumentOutOfRangeException(paramName,$"{paramName} must be positive.");
        if((value&(value-1))!=0)
            throw new ArgumentOutOfRangeException(paramName,$"{paramName} must be a power of 2, but was {value}.");
    }

    private protected abstract long GetRecoveryEntryCount();
}



public abstract partial class CollectionStore<TKey, TValue>
   :CollectionStore<
        TKey,
        TValue,
        SpanByteStoreFunctions,
        SpanByteStoreAllocator
    > {
}



public abstract partial class CollectionStore<TKey, TValue> {
    private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        base(
            new SpanByteStoreFunctions(new SpanByteComparer(),static () => null,DefaultRecordTriggers.Instance),
            static (allocSettings,storeFuncs) => new SpanByteStoreAllocator(allocSettings,storeFuncs),
            IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind,StorageLimitFraction,StorageReserveBytes
        ) {
    }



















}




/// <summary>
/// CollectionStoreの境界条件を検証する。
/// CollectionStoreの境界条件を検証する。
/// </summary>
public abstract partial class CollectionStore<
    TKey,
    TValue,
    TStoreFunctions,
    TAllocator
>
    :CollectionStore, IDisposable    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions> {
    private protected KVSettings Settings { get; private set; }
    public readonly TsavoriteKV<TStoreFunctions,TAllocator> Store;
    public long LogMemorySize { get; }
    public double MutableFraction { get; }
    internal OverflowMutableStrategyKind OverflowMutableStrategyKind { get; }
    internal StorageOverflowMutableStrategy StorageOverflowMutableStrategy { get; }
    private bool Disposed;
    private int CheckpointInProgress;
    private bool UsesStorageDevice => this.Settings is not null&&(this.Settings.LogDevice is not MemoryOnlyDevice||this.Settings.ObjectLogDevice is not MemoryOnlyDevice);

    /// <summary>CollectionStoreの境界条件を検証する。</summary>
    private protected CollectionStore(
        TStoreFunctions StoreFunctions,
        Func<AllocatorSettings,TStoreFunctions,TAllocator> allocatorFactory,
        long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) {
        ValidatePowerOfTwo(IndexSize,nameof(IndexSize));
        ValidatePowerOfTwo(PageSize,nameof(PageSize));
        ValidatePowerOfTwo(SegmentSize,nameof(SegmentSize));
        ValidatePowerOfTwo(MemorySize,nameof(MemorySize));
        AssertIsPowerOfTwo(IndexSize,nameof(IndexSize));
        AssertIsPowerOfTwo(PageSize,nameof(PageSize));
        AssertIsPowerOfTwo(SegmentSize,nameof(SegmentSize));
        AssertIsPowerOfTwo(MemorySize,nameof(MemorySize));
        if(IndexSize<64)
            throw new ArgumentOutOfRangeException(nameof(IndexSize),$"{nameof(IndexSize)} must be >= 64.");
        if(PageSize<1L<<12)
            throw new ArgumentOutOfRangeException(nameof(PageSize),$"{nameof(PageSize)} must be >= 4096.");
        if(PageSize>1L<<30)
            throw new ArgumentOutOfRangeException(nameof(PageSize),$"{nameof(PageSize)} must be <= 1GB (1L<<30).");
        if(PageSize>SegmentSize)
            throw new ArgumentOutOfRangeException(nameof(SegmentSize),"SegmentSize must be at least equal to PageSize.");
        if(MemorySize<2*PageSize)
            throw new ArgumentOutOfRangeException(nameof(MemorySize),$"{nameof(MemorySize)} must be >= 2*{nameof(PageSize)}.");
        if(SegmentSize>1L<<62)
            throw new ArgumentOutOfRangeException(nameof(SegmentSize),$"{nameof(SegmentSize)} must be <= 1L<<62.");
        if(double.IsNaN(MutableFraction)||MutableFraction<0||MutableFraction>1)
            throw new ArgumentOutOfRangeException(nameof(MutableFraction));
        if(ObjectLogDevice.Capacity!=-1&&(ObjectLogDevice.Capacity<Const.MinObjectLogCapacity||ObjectLogDevice.Capacity>Const.MaxObjectLogCapacity))
            throw new ArgumentOutOfRangeException(nameof(ObjectLogDevice),$"{nameof(ObjectLogDevice)} capacity must be -1 or between {Const.MinObjectLogCapacity} and {Const.MaxObjectLogCapacity}.");
        this.LogMemorySize=MemorySize;
        this.MutableFraction=MutableFraction;
        this.OverflowMutableStrategyKind=OverflowMutableStrategyKind;
        this.StorageOverflowMutableStrategy=new(LogDevice.Capacity,StorageLimitFraction,StorageReserveBytes);
        long objectLogSegmentSize;
        if(ObjectLogDevice.Capacity<0) {
            objectLogSegmentSize=Const.MinObjectLogCapacity;
        }
        else {
            if(ObjectLogDevice.Capacity%SegmentSize!=0)
                throw new ArgumentOutOfRangeException(nameof(ObjectLogDevice),$"ObjectLogDevice.Capacity must be a multiple of SegmentSize ({SegmentSize}), but was {ObjectLogDevice.Capacity}.");
            objectLogSegmentSize=ObjectLogDevice.Capacity;
        }
        this.Settings=new() {
            IndexSize=IndexSize,
            LogMemorySize=MemorySize,
            PageSize=PageSize,
            SegmentSize=SegmentSize,

            MutableFraction=MutableFraction,
            LogDevice=LogDevice,
            ObjectLogDevice=ObjectLogDevice,
            ObjectLogSegmentSize=objectLogSegmentSize,
            CheckpointDir=CheckpointDir,
        };
        this.Store=new TsavoriteKV<TStoreFunctions,TAllocator>(
            this.Settings,
            StoreFunctions,
            allocatorFactory
        );
    }

    private protected CollectionStore(
        TStoreFunctions StoreFunctions,
        Func<AllocatorSettings,TStoreFunctions,TAllocator> allocatorFactory,
        long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction)
       : this(StoreFunctions,allocatorFactory,IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
    }

    public void AssertCheckpointDir() {
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
    }

    public async ValueTask<Guid> SaveAsync(CheckpointType CheckpointType = CheckpointType.Snapshot,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        this.ThrowIfCheckpointInProgress();
        try {
            var (success,token)=await this.Store.TakeFullCheckpointAsync(CheckpointType,ct);
            await this.Store.CompleteCheckpointAsync(ct);
            if(success) {
                this.WriteRecoveryIndexMetadata(token);
                return token;
            }
            throw new InvalidOperationException($"{nameof(Save)}Checkpoint failed.");
        }
        finally {
            this.ReleaseCheckpoint();
        }
    }

    public Guid Save(CheckpointType CheckpointType = CheckpointType.Snapshot) => this.SaveAsync(CheckpointType).GetAwaiter().GetResult();

    public async ValueTask<Guid> SaveLogOnlyAsync(CheckpointType CheckpointType = CheckpointType.Snapshot,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        this.ThrowIfCheckpointInProgress();
        try {
            var (success,token)=await this.Store.TakeHybridLogCheckpointAsync(CheckpointType,cancellationToken: ct);
            await this.Store.CompleteCheckpointAsync(ct);
            if(success) {
                this.WriteRecoveryIndexMetadata(token);
                return token;
            }
            throw new InvalidOperationException($"{nameof(SaveLogOnly)}Checkpoint failed.");
        }
        finally {
            this.ReleaseCheckpoint();
        }
    }

    private void ThrowIfCheckpointInProgress() {
        if(Interlocked.CompareExchange(ref this.CheckpointInProgress,1,0)!=0)
            throw new InvalidOperationException("Checkpoint already in progress.");
    }

    private void ReleaseCheckpoint() {
        Volatile.Write(ref this.CheckpointInProgress,0);
    }

    private void WriteRecoveryIndexMetadata(Guid token) {
        global::Seamless.Tsavorite.Collections.Store.WriteRecoveryIndexMetadata(this.Settings.CheckpointDir!,token,this.GetRecoveryEntryCount());
    }

    public Guid SaveLogOnly(CheckpointType CheckpointType = CheckpointType.Snapshot) => this.SaveLogOnlyAsync(CheckpointType).GetAwaiter().GetResult();

    public async ValueTask LoadAsync(CancellationToken ct = default) {
        this.AssertCheckpointDir();
        await this.Store.RecoverAsync(cancellationToken: ct);
    }

    public void Load() => this.LoadAsync().GetAwaiter().GetResult();

    public async ValueTask LoadAsync(Guid token,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        await this.Store.RecoverAsync(Guid.Empty,token,cancellationToken: ct);
    }

    public void Load(Guid token) {
        var ct = CancellationToken.None;
        this.LoadAsync(token,ct).GetAwaiter().GetResult();
    }

    public (long BeginAddress,long HeadAddress,long ReadOnlyAddress,long TailAddress) Address {
        get {
            var Log = this.Store.Log;
            return (Log.BeginAddress,Log.HeadAddress,Log.ReadOnlyAddress,Log.TailAddress);
        }
    }

    public void CompactWithLookup() {
        var store = this.Store;
        var beginAddress = store.Log.BeginAddress;
        var segmentSize = this.Settings.SegmentSize;
        var nextSegmentStart = (beginAddress/segmentSize+1)*segmentSize;
        var compactUntil = nextSegmentStart;
        var safeReadOnlyAddress = store.Log.SafeReadOnlyAddress;
        if(compactUntil>safeReadOnlyAddress) {
            compactUntil=safeReadOnlyAddress;
        }
        if(compactUntil<=beginAddress) {
            return;
        }
        var untilAddress = this.Store.Log.Compact<TKey,TValue,EqualityEmpty>(
            compactUntil,
            CompactionType.Lookup
        );
        store.Log.ShiftBeginAddress(untilAddress,true,true);
    }

    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if(this.Disposed) return;
        this.Disposed=true;
        if(disposing) {
            if(this.Store is not null&&this.UsesStorageDevice) {
                this.Store.Log.FlushAndEvict(wait: true);
            }
        }
        this.Store?.Dispose();
        this.Settings?.Dispose();
    }

    ~CollectionStore() => this.Dispose(false);
}
