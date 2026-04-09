namespace Seamless.Store;

public abstract class ManagedCollectionStore<TKey,TValue>:CollectionStore<TKey,TValue,
    SpanByte,SpanByte,
    StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,
    SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
    private protected ManagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (
            new StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>(new SpanByteComparer(),null!,null!,new SpanByteRecordDisposer()),
            static (allocSettings,storeFuncs) => new SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>(allocSettings,storeFuncs),
            IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){}

















    protected override long CompactLog(long compactUntil) =>
        this.Store.Log.Compact<SpanByte,SpanByteAndMemory,Tsavorite.core.Empty,SessionFunctions.Add>(
            new(),
            compactUntil,
            CompactionType.Lookup
        );
}
