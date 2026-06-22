namespace Seamless.Tsavorite.Collections.Stores;

public abstract partial class CollectionStore<TKey, TValue>
   :CollectionStore<
        TKey,
        TValue,
        SpanByteStoreFunctions,
        SpanByteStoreAllocator
    > {
}