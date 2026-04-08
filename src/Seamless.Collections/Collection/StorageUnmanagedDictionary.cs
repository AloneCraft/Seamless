namespace Seamless.Collections;

public sealed class StorageUnmanagedDictionary<TKey,TValue>:UnmanagedDictionary<TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    internal StorageUnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
    }

    private protected override bool WouldOverflowMutable(int keyLength,int valueLength) => false;
}
