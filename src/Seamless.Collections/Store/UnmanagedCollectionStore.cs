namespace Seamless.Store;
public abstract partial class UnmanagedCollectionStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> { }
