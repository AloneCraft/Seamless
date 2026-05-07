using Seamless.Collections;
using Seamless.Devices;

namespace Seamless.Stores;
/// <summary>
/// UnmanagedHashSet 用の Tsavorite ストア。
/// </summary>
public class UnmanagedHashSetStore<TKey>
    :UnmanagedCollectionStore<TKey,EqualityEmpty>
    where TKey : unmanaged {
    internal UnmanagedHashSetStore(
        long IndexSize,
        long PageSize,
        long SegmentSize,
        long MemorySize,
        double MutableFraction,
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        string? CheckpointDir,
        double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,
        long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes
    ) : base(
        IndexSize,
        PageSize,
        SegmentSize,
        MemorySize,
        MutableFraction,
        LogDevice,
        ObjectLogDevice,
        CheckpointDir,
        OverflowMutableStrategyKind.Storage,
        StorageLimitFraction,
        StorageReserveBytes
    ) { }

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
        new MemoryOnlyDevice(SegmentSize*2),
        new MemoryOnlyDevice(Const.MinObjectLogCapacity),
        null,
        OverflowMutableStrategyKind.Memory
    ) { }

    private protected override long GetRecoveryEntryCount() {
        using var set = this.Create();
        return set.GetLongCount();
    }

    public Seamless.Collections.HashSet<TKey> Create() => new(this);
    internal Seamless.Collections.HashSet<TKey> CreateOwned() => new(this,this);
}

//public class UnmanagedUnmanagedHashSetStore<TKey>:UnmanagedHashSetStore<TKey>
//    where TKey : unmanaged {
//    internal UnmanagedUnmanagedHashSetStore(
//        long IndexSize,
//        long PageSize,
//        long SegmentSize,
//        long MemorySize,
//        double MutableFraction,
//        IDevice LogDevice,
//        IDevice ObjectLogDevice,
//        string? CheckpointDir
//    ) : base(
//        IndexSize,
//        PageSize,
//        SegmentSize,
//        MemorySize,
//        MutableFraction,
//        LogDevice,
//        ObjectLogDevice,
//        CheckpointDir
//    ){ }

//    internal UnmanagedUnmanagedHashSetStore(
//        long IndexSize,
//        long PageSize,
//        long SegmentSize,
//        long MemorySize,
//        double MutableFraction
//    ) : base(
//        IndexSize,
//        PageSize,
//        SegmentSize,
//        MemorySize,
//        MutableFraction
//    ){ }

//    public new UnmanagedUnmanagedHashSet<TKey> Create() => new(this);
//}
