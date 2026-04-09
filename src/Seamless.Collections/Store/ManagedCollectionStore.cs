namespace Seamless.Store;
public abstract partial class ManagedCollectionStore<TKey, TValue>
    : CollectionStore<
        TKey,
        TValue,
#if TSAVORITE1_1_1
        ManagedSessionKey,
        ManagedSessionValue,
#endif
        ManagedStoreFunctions,
        ManagedAllocator
    >
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> { }
