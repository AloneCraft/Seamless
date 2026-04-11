namespace Seamless.ScanIteratorFunctions;

public sealed class UnmanagedKeyIterator<TKey,TValue>:ScanIteratorFunctions<TKey,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TKey Deserialize(ref TKey key,ref TValue value) => key;
}
