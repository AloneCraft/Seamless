namespace Seamless.Store;
public abstract partial class ManagedCollectionStore<TKey, TValue>
    : CollectionStore<TKey, TValue, ManagedSessionKey, ManagedSessionValue, ManagedStoreFunctions, ManagedAllocator>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> { }
