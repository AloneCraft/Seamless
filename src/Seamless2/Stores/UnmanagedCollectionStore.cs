namespace Seamless.Stores;

public abstract partial class UnmanagedCollectionStore<TKey, TValue>:CollectionStore<
    TKey,
    TValue,
    UnmanagedStoreFunctions,
    UnmanagedAllocator>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    private protected UnmanagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        base(
            new UnmanagedStoreFunctions(new SpanByteComparer(),static () => null,new DefaultRecordDisposer()),
            static (allocSettings,storeFuncs) => new UnmanagedAllocator(allocSettings,storeFuncs),
            IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind,StorageLimitFraction,StorageReserveBytes
        ) {
    }







}

