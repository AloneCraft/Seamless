namespace Seamless.Stores;

public abstract partial class ManagedCollectionStore<TKey,TValue>{
    private protected ManagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind):
        base(
            new ManagedStoreFunctions(new SpanByteComparer(),null!,null!,new SpanByteRecordDisposer()),
            static(allocSettings,storeFuncs)=>new ManagedAllocator(allocSettings,storeFuncs),
            IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind
        ){
    }

















    protected override long CompactLog(long compactUntil)=>
        this.Store.Log.Compact<SpanByte,bool,EqualityEmpty,SessionFunctions.ManagedAdd>(
            SessionFunctions.ManagedAdd.Create(),
            compactUntil,
            CompactionType.Lookup
        );
}
