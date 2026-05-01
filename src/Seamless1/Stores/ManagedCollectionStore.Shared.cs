namespace Seamless.Stores;

public abstract partial class ManagedCollectionStore<TKey, TValue>
   :CollectionStore<
        TKey,
        TValue,
        ManagedSessionValue,
        ManagedSessionValue,
        ManagedStoreFunctions,
        ManagedAllocator
    > {
}