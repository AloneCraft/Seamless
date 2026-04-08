using Tsavorite.core;

namespace Seamless.Collections;

#if TSAVORITE2_0BETA3
public abstract class UnmanagedCollectionStore<TKey,TValue>:CollectionStore<TKey,TValue,
    PinnedSpanByte,SpanByteAndMemory,
    StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,
    SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected UnmanagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (default!,null!,IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){}
}
#elif TSAVORITE1_1_1
public abstract class UnmanagedCollectionStore<TKey,TValue>:CollectionStore<
    TKey,
    TValue,
    TKey,
    TValue,
    StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>,
    BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected UnmanagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base(
            new StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>(
                new UnmanagedKeyComparer<TKey>(),
                null!,
                null!,
                DefaultRecordDisposer<TKey,TValue>.Instance
            ),
            static (allocSettings,storeFunctions) => new BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>(allocSettings,storeFunctions),
            IndexSize,
            PageSize,
            SegmentSize,
            MemorySize,
            MutableFraction,
            LogDevice,
            ObjectLogDevice,
            CheckpointDir) {
    }

    public override void CompactWithLookup() {
        var store = this.Store;
        var beginAddress = store.Log.BeginAddress;
        var segmentSize = this.Settings.SegmentSize;
        var nextSegmentStart = (beginAddress/segmentSize+1)*segmentSize;
        var compactUntil = nextSegmentStart;
        var safeReadOnlyAddress = store.Log.SafeReadOnlyAddress;
        if(compactUntil>safeReadOnlyAddress) {
            compactUntil=safeReadOnlyAddress;
        }
        if(compactUntil<=beginAddress) {
            return;
        }
        var untilAddress = store.Log.Compact<TValue,TValue,Tsavorite.core.Empty,Seamless.SessionFunctions.UnmanagedAdd<TKey,TValue>>(
            new(),
            compactUntil,
            CompactionType.Lookup
        );
        store.Log.ShiftBeginAddress(untilAddress,true,true);
    }
}
#endif
