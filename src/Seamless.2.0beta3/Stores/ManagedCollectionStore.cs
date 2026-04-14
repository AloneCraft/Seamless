namespace Seamless.Stores;

public abstract partial class ManagedCollectionStore<TKey,TValue>{
    private protected ManagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind):
        base(
            new StoreFunctions<SpanByteComparer,DefaultRecordDisposer>(new SpanByteComparer(),static()=>null,new DefaultRecordDisposer()),
            static(allocSettings,storeFuncs)=>new SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>(allocSettings,storeFuncs),
            IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind
        ){
    }



















}
