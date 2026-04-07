using Seamless.Devices;
using Seamless.SessionFunctions;
using System.Diagnostics;
using Tsavorite.core;

namespace Seamless.Collections;

#if TSAVORITE2_0BETA3
public abstract class UnmanagedCollectionStore<TKey, TValue> : CollectionStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    private protected UnmanagedCollectionStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction, IDevice LogDevice, IDevice ObjectLogDevice, string? CheckpointDir)
        : base(IndexSize, PageSize, SegmentSize, MemorySize, MutableFraction, LogDevice, ObjectLogDevice, CheckpointDir) { }
}

#elif TSAVORITE1_1_1

/// <summary>
/// unmanaged 固定長型用 BlittableAllocator ストア（TSAVORITE1_1_1 専用）。
/// SpanByte の長さプレフィックスを持たない固定長レコードで格納する。
/// </summary>
public abstract class UnmanagedCollectionStore<TKey, TValue> : CollectionStore, IDisposable
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    private KVSettings<TKey, TValue> Settings;
    public readonly TsavoriteKV<TKey, TValue,
        StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>,
        BlittableAllocator<TKey, TValue, StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>>> Store;

    public long LogMemorySize { get; }
    public double MutableFraction { get; }
    private bool Disposed;

    private protected UnmanagedCollectionStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction, IDevice LogDevice, IDevice ObjectLogDevice, string? CheckpointDir)
    {
        AssertIsPowerOfTwo(IndexSize, nameof(IndexSize));
        AssertIsPowerOfTwo(PageSize, nameof(PageSize));
        AssertIsPowerOfTwo(SegmentSize, nameof(SegmentSize));
        AssertIsPowerOfTwo(MemorySize, nameof(MemorySize));
        Debug.Assert(MemorySize >= 2 * PageSize, $"{nameof(MemorySize)} must be >= 2*{nameof(PageSize)}");
        this.LogMemorySize = MemorySize;
        this.MutableFraction = MutableFraction;
        this.Settings = new KVSettings<TKey, TValue>
        {
            IndexSize = IndexSize,
            MemorySize = MemorySize,
            PageSize = PageSize,
            SegmentSize = SegmentSize,
            MutableFraction = MutableFraction,
            LogDevice = LogDevice,
            ObjectLogDevice = ObjectLogDevice,
            CheckpointDir = CheckpointDir,
        };
        var storeFunctions = new StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>(
            new UnmanagedKeyComparer<TKey>(),
            null!,
            null!,
            DefaultRecordDisposer<TKey, TValue>.Instance);
        this.Store = new TsavoriteKV<TKey, TValue,
            StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>,
            BlittableAllocator<TKey, TValue, StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>>>(
            this.Settings,
            storeFunctions,
            static (allocSettings, storeFuncs) =>
                new BlittableAllocator<TKey, TValue, StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>>(allocSettings, storeFuncs));
    }

    public (long BeginAddress, long HeadAddress, long ReadOnlyAddress, long TailAddress) Address
    {
        get
        {
            var log = this.Store.Log;
            return (log.BeginAddress, log.HeadAddress, log.ReadOnlyAddress, log.TailAddress);
        }
    }

    private void AssertCheckpointDir()
    {
        if (string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
    }

    public async ValueTask<Guid> SaveAsync(CheckpointType checkpointType = CheckpointType.Snapshot, CancellationToken ct = default)
    {
        this.AssertCheckpointDir();
        await Task.Yield();
        var (success, token) = await this.Store.TakeFullCheckpointAsync(checkpointType, ct);
        await this.Store.CompleteCheckpointAsync(ct);
        if (success) return token;
        throw new InvalidOperationException("Checkpoint failed.");
    }

    public Guid Save(CheckpointType checkpointType = CheckpointType.Snapshot) =>
        this.SaveAsync(checkpointType).GetAwaiter().GetResult();

    public async ValueTask LoadAsync(CancellationToken ct = default)
    {
        this.AssertCheckpointDir();
        await this.Store.RecoverAsync(cancellationToken: ct);
    }

    public void Load() => this.LoadAsync().GetAwaiter().GetResult();

    public void CompactWithLookup()
    {
        var store = this.Store;
        var beginAddress = store.Log.BeginAddress;
        var segmentSize = this.Settings.SegmentSize;
        var nextSegmentStart = (beginAddress / segmentSize + 1) * segmentSize;
        var compactUntil = Math.Min(nextSegmentStart, store.Log.SafeReadOnlyAddress);
        if (compactUntil <= beginAddress) return;
        var untilAddress = store.Log.Compact<TValue, TValue, Empty, UnmanagedAdd<TKey, TValue>>(
            new(),
            compactUntil,
            CompactionType.Lookup);
        store.Log.ShiftBeginAddress(untilAddress, true, true);
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.Disposed) return;
        this.Disposed = true;
        if (disposing)
        {
            this.Store.Dispose();
            this.Settings.Dispose();
        }
    }

    ~UnmanagedCollectionStore() => this.Dispose(false);
}
#endif

public abstract class UnmanagedDictionaryStore<TKey, TValue> : UnmanagedCollectionStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    private protected UnmanagedDictionaryStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction, IDevice LogDevice, IDevice ObjectLogDevice, string? CheckpointDir)
        : base(IndexSize, PageSize, SegmentSize, MemorySize, MutableFraction, LogDevice, ObjectLogDevice, CheckpointDir) { }

    public abstract UnmanagedDictionary<TKey, TValue> Create();
}

public sealed class MemoryUnmanagedDictionaryStore<TKey, TValue> : UnmanagedDictionaryStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    internal MemoryUnmanagedDictionaryStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction)
        : base(IndexSize, PageSize, SegmentSize, MemorySize, MutableFraction, new MemoryOnlyDevice(SegmentSize * 2), new MemoryOnlyDevice(Const.MinObjectLogCapacity), null) { }

    public override UnmanagedDictionary<TKey, TValue> Create() => new MemoryUnmanagedDictionary<TKey, TValue>(this);
}

public sealed class StorageUnmanagedDictionaryStore<TKey, TValue> : UnmanagedDictionaryStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    internal StorageUnmanagedDictionaryStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction, IDevice LogDevice, IDevice ObjectLogDevice, string? CheckpointDir)
        : base(IndexSize, PageSize, SegmentSize, MemorySize, MutableFraction, LogDevice, ObjectLogDevice, CheckpointDir) { }

    public override UnmanagedDictionary<TKey, TValue> Create() => new StorageUnmanagedDictionary<TKey, TValue>(this);
}
