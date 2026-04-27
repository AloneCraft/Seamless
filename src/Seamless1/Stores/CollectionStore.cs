using System.Diagnostics;
using Seamless.Devices;

namespace Seamless.Stores;

/// <summary>
/// Tsavorite ストアを管理する抽象基底クラス。
/// DictionaryStore および HashSetStore の共通実装を提供する。
/// </summary>
public abstract partial class CollectionStore<
    TKey,
    TValue,
    TSessionKey,
    TSessionValue,
    TStoreFunctions,
    TAllocator
>
    :CollectionStore, IDisposable
    where TKey : IEquatable<TKey>
    where TStoreFunctions : IStoreFunctions<TSessionKey,TSessionValue>
    where TAllocator : IAllocator<TSessionKey,TSessionValue,TStoreFunctions> {
    private KVSettings<TSessionKey,TSessionValue> Settings { get; }
    public readonly TsavoriteKV<TSessionKey,TSessionValue,TStoreFunctions,TAllocator> Store;
    public long LogMemorySize { get; }
    public double MutableFraction { get; }
    internal OverflowMutableStrategyKind OverflowMutableStrategyKind { get; }
    private bool Disposed;
    private int CheckpointInProgress;
    private bool UsesStorageDevice => this.Settings is not null&&(this.Settings.LogDevice is not MemoryOnlyDevice||this.Settings.ObjectLogDevice is not MemoryOnlyDevice);

    /// <summary>テスト専用シーム: TakeCheckpointAsync 完了後・CompleteCheckpointAsync 前に呼び出されるコールバック。</summary>
    private protected CollectionStore(
        TStoreFunctions StoreFunctions,
        Func<AllocatorSettings,TStoreFunctions,TAllocator> allocatorFactory,
        long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind) {
        AssertIsPowerOfTwo(IndexSize,nameof(IndexSize));
        AssertIsPowerOfTwo(PageSize,nameof(PageSize));
        AssertIsPowerOfTwo(SegmentSize,nameof(SegmentSize));
        AssertIsPowerOfTwo(MemorySize,nameof(MemorySize));
        Debug.Assert(IndexSize>=64,$"{nameof(IndexSize)} must be >= 64");
        Debug.Assert(PageSize>=1L<<12,$"{nameof(PageSize)} must be >= 4096");
        Debug.Assert(PageSize<=1L<<30,$"{nameof(PageSize)} must be <= 1GB (1L<<30)");
        Debug.Assert(PageSize<=SegmentSize,"SegmentSize must be at least equal to PageSize.");
        Debug.Assert(MemorySize>=2*PageSize,$"{nameof(MemorySize)} must be >= 2*{nameof(PageSize)}");
        Debug.Assert(SegmentSize<=1L<<62,$"{nameof(SegmentSize)} must be <= 1L<<62");
        Debug.Assert(ObjectLogDevice.Capacity is >=Const.MinObjectLogCapacity and <=Const.MaxObjectLogCapacity or -1);
        this.LogMemorySize=MemorySize;
        this.MutableFraction=MutableFraction;
        this.OverflowMutableStrategyKind=OverflowMutableStrategyKind;
        this.Settings=new() {
            IndexSize=IndexSize,
            MemorySize=MemorySize,
            PageSize=PageSize,
            SegmentSize=SegmentSize,
            MutableFraction=MutableFraction,
            LogDevice=LogDevice,
            ObjectLogDevice=ObjectLogDevice,

            CheckpointDir=CheckpointDir,
        };
        if(
            OverflowMutableStrategyKind==OverflowMutableStrategyKind.Storage
            &&typeof(TAllocator).IsGenericType
            &&typeof(TAllocator).GetGenericTypeDefinition()==typeof(BlittableAllocator<,,>)
        ) {
            this.Settings.ReadCopyOptions=new ReadCopyOptions {
                CopyFrom=ReadCopyFrom.Device,
                CopyTo=ReadCopyTo.ReadCache,
            };
            this.Settings.ReadCacheEnabled=true;
            this.Settings.ReadCachePageSize=PageSize;
            this.Settings.ReadCacheMemorySize=MemorySize;
        }
        this.Store=new TsavoriteKV<TSessionKey,TSessionValue,TStoreFunctions,TAllocator>(
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
            throw new InvalidOperationException($"{nameof(this.Save)}Checkpoint failed.");
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
            throw new InvalidOperationException($"{nameof(this.SaveLogOnly)}Checkpoint failed.");
        }
        finally {
            this.ReleaseCheckpoint();
        }
    }

    private void ThrowIfCheckpointInProgress() {
        if(System.Threading.Interlocked.CompareExchange(ref this.CheckpointInProgress,1,0)!=0)
            throw new InvalidOperationException("Checkpoint already in progress.");
    }

    private void ReleaseCheckpoint() {
        System.Threading.Volatile.Write(ref this.CheckpointInProgress,0);
    }

    private void WriteRecoveryIndexMetadata(Guid token) {
        HashStore.WriteRecoveryIndexMetadata(this.Settings.CheckpointDir!,token,this.GetRecoveryEntryCount());
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
        var untilAddress = this.CompactLog(compactUntil);
        store.Log.ShiftBeginAddress(untilAddress,true,true);
    }

    protected abstract long CompactLog(long compactUntil);

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