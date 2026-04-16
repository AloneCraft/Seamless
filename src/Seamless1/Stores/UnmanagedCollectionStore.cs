using Seamless.Collections;
namespace Seamless.Stores;

public abstract partial class UnmanagedCollectionStore<TKey,TValue>:CollectionStore<
    TKey,
    TValue,
    TKey,
    TValue,
    StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>,
    BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>{
    private protected UnmanagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind):
        base(
            new StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>(
                new UnmanagedKeyComparer<TKey>(),
                null!,
                null!,
                DefaultRecordDisposer<TKey,TValue>.Instance
            ),
            static(allocSettings,storeFunctions)=>new BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>(allocSettings,storeFunctions),
            IndexSize,
            PageSize,
            SegmentSize,
            MemorySize,
            MutableFraction,
            LogDevice,
            ObjectLogDevice,
            CheckpointDir,
            OverflowMutableStrategyKind
        ){
    }

    protected override long CompactLog(long compactUntil)=>this.Store.Log.Compact<TValue,bool,EqualityEmpty,SessionFunctions.UnmanagedAdd<TKey,TValue>>(
        new(),
        compactUntil,
        CompactionType.Lookup
    );
}
