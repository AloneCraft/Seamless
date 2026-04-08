using Tsavorite.core;

namespace Seamless.Collections;

#if TSAVORITE2_0BETA3
public abstract class ManagedCollectionStore<TKey,TValue>:CollectionStore<TKey,TValue,
    PinnedSpanByte,SpanByteAndMemory,
    StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,
    SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
    private protected ManagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (default!,null!,IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}
}
#elif TSAVORITE1_1_1
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

    public override void CompactWithLookup() {
        var Store = this.Store;
        var BeginAddress = Store.Log.BeginAddress;
        var SegmentSize = this.Settings.SegmentSize;
        var NextSegmentStart = (BeginAddress/SegmentSize+1)*SegmentSize;
        var CompactUntil = NextSegmentStart;
        var SafeReadOnlyAddress = Store.Log.SafeReadOnlyAddress;
        if(CompactUntil>SafeReadOnlyAddress) {
            CompactUntil=SafeReadOnlyAddress;
        }
        if(CompactUntil<=BeginAddress)
            return;
        var UntilAddress = Store.Log.Compact<SpanByte, SpanByteAndMemory, Tsavorite.core.Empty, SessionFunctions.Add>(
            new(),
            CompactUntil,
            CompactionType.Lookup
        );
        Store.Log.ShiftBeginAddress(UntilAddress,true,true);
    }
}
#endif
