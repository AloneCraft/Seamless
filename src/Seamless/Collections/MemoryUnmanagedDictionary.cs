using Seamless.Stores;
namespace Seamless.Collections;

public sealed class MemoryUnmanagedDictionary<TKey,TValue>:UnmanagedDictionary<
    TKey,
    TValue,
    UnmanagedSerializer<TKey>,
    UnmanagedSerializer<TValue>
>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    internal MemoryUnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
    }

    private protected override bool WouldOverflowMutable(int keyLength,int valueLength) => this.PrivateProtectedWouldOverflowMutable(keyLength,valueLength);
}
