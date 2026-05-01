using Seamless.Collections;
namespace Seamless.Stores;

public abstract partial class UnmanagedCollectionStore<TKey, TValue>:CollectionStore<
    TKey,
    TValue,
    TKey,
    TValue,
    StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>,
    BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>>
    where TKey : unmanaged
    where TValue : unmanaged {
    private protected UnmanagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
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
            CheckpointDir,
            OverflowMutableStrategyKind,
            StorageLimitFraction,
            StorageReserveBytes
        ) {
    }

    protected override long CompactLog(long compactUntil) => this.Store.Log.Compact<TValue,bool,EqualityEmpty,SessionFunctions.UnmanagedAdd<TKey,TValue>>(
        new(),
        compactUntil,
        CompactionType.Lookup
    );
}
