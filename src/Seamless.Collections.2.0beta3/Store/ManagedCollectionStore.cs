namespace Seamless.Collections;

public abstract class ManagedCollectionStore<TKey,TValue>:CollectionStore<TKey,TValue,
    PinnedSpanByte,SpanByteAndMemory,
    StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,
    SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
    private protected ManagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (default!,null!,IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}
}
