namespace Seamless.Collections.Tsavorite.Stores;

public abstract partial class CollectionStore<TKey, TValue> {
    private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        base(
            new SpanByteStoreFunctions(new SpanByteComparer(),static () => null,DefaultRecordTriggers.Instance),
            static (allocSettings,storeFuncs) => new SpanByteStoreAllocator(allocSettings,storeFuncs),
            IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind,StorageLimitFraction,StorageReserveBytes
        ) {
    }



















}
