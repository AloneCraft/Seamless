namespace Seamless.Collections;

public abstract class UnmanagedCollectionStore<TKey,TValue>:CollectionStore<TKey,TValue,
    PinnedSpanByte,SpanByteAndMemory,
    StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,
    SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected UnmanagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (default!,null!,IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){}
}
