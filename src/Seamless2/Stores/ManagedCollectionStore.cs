namespace Seamless.Stores;

public abstract partial class ManagedCollectionStore<TKey, TValue> {
    private protected ManagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        base(
            new ManagedStoreFunctions(new SpanByteComparer(),static () => null,new DefaultRecordDisposer()),
            static (allocSettings,storeFuncs) => new UnmanagedAllocator(allocSettings,storeFuncs),
            IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind,StorageLimitFraction,StorageReserveBytes
        ) {
    }



















}
