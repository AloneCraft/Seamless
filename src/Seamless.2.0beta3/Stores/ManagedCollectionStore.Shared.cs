namespace Seamless.Stores;

public abstract partial class ManagedCollectionStore<TKey, TValue>
   :CollectionStore<
        TKey,
        TValue,
        ManagedStoreFunctions,
        ManagedAllocator
    >
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
}