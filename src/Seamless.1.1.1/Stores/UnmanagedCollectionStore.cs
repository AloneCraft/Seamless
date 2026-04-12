namespace Seamless.Stores;

public abstract partial class UnmanagedCollectionStore<TKey,TValue>:CollectionStore<
    TKey,
    TValue,
    TKey,
    TValue,
    StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,TValue>>,
    BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,TValue>>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected UnmanagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base(
            new StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,TValue>>(
                new UnmanagedKeyComparer<TKey>(),
                null!,
                null!,
                UnmanagedRecordDisposer<TKey,TValue>.Instance
            ),
            static (allocSettings,storeFunctions) => new BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,TValue>>>(allocSettings,storeFunctions),
            IndexSize,
            PageSize,
            SegmentSize,
            MemorySize,
            MutableFraction,
            LogDevice,
            ObjectLogDevice,
            CheckpointDir) {
    }

    protected override long CompactLog(long compactUntil) =>
        this.Store.Log.Compact<TValue,bool,EqualityEmpty,SessionFunctions.UnmanagedAdd<TKey,TValue>>(
            new(),
            compactUntil,
            CompactionType.Lookup
        );
}
