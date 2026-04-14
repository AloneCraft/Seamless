using Seamless.Collections;
using Seamless.Devices;

namespace Seamless.Stores;
/// <summary>
/// UnmanagedHashSet 用の Tsavorite ストア。
/// </summary>
public class UnmanagedHashSetStore<TKey>
    : UnmanagedCollectionStore<TKey, EqualityEmpty>
    where TKey : unmanaged, IEquatable<TKey>
{
    internal UnmanagedHashSetStore(
        long IndexSize,
        long PageSize,
        long SegmentSize,
        long MemorySize,
        double MutableFraction,
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        string? CheckpointDir
    ) : base(
        IndexSize,
        PageSize,
        SegmentSize,
        MemorySize,
        MutableFraction,
        LogDevice,
        ObjectLogDevice,
        CheckpointDir,
        OverflowMutableStrategyKind.Storage
    )
    { }

    internal UnmanagedHashSetStore(
        long IndexSize,
        long PageSize,
        long SegmentSize,
        long MemorySize,
        double MutableFraction
    ) : base(
        IndexSize,
        PageSize,
        SegmentSize,
        MemorySize,
        MutableFraction,
        new MemoryOnlyDevice(SegmentSize * 2),
        new MemoryOnlyDevice(Const.MinObjectLogCapacity),
        null,
        OverflowMutableStrategyKind.Memory
    )
    { }

    public UnmanagedHashSet<TKey> Create() => new(this);
}
