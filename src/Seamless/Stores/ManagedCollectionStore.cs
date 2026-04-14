namespace Seamless.Stores;

public abstract partial class ManagedCollectionStore<TKey, TValue>
    : CollectionStore<
        TKey,
        TValue,
#if TSAVORITE1_1_1
        SpanByte,
        SpanByte,
#endif
        ManagedStoreFunctions,
        ManagedAllocator
    >
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{ }
//public abstract partial class CollectionStore<TKey,TValue,TSessionKey,TSessionValue,TStoreFunctions,TAllocator>

