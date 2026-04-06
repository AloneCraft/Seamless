namespace Seamless.Collections;

public sealed class StorageUnmanagedDictionary<TKey, TValue> : UnmanagedDictionary<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    public StorageUnmanagedDictionary(StorageUnmanagedDictionaryStore<TKey, TValue> Store) : base(Store) { }

    private protected override bool WouldOverflowMutable(int KeyLength, int ValueLength) => false;
}
