using Seamless.Collections;
namespace Seamless.Store;

public abstract partial class UnmanagedCollectionStore<TKey,TValue>:CollectionStore<
    TKey,
    TValue,
    StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>,
    SpanByteAllocator<StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected UnmanagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (new StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>(new UnmanagedKeyComparer<TKey>(),static ()=>null,new SpanByteRecordDisposer()),
                                     static (allocSettings,storeFuncs)=>new SpanByteAllocator<StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>>(allocSettings,storeFuncs),
                                     IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){}
}
