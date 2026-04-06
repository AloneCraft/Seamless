namespace Seamless.Collections;

public sealed class MemoryUnmanagedDictionary<TKey, TValue> : UnmanagedDictionary<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    internal MemoryUnmanagedDictionary(MemoryUnmanagedDictionaryStore<TKey, TValue> CollectionStore) : base(CollectionStore) { }

    private protected override bool WouldOverflowMutable(int KeyLength, int ValueLength) =>
        this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
