using Tsavorite.core;

namespace Seamless.ScanIteratorFunctions;

internal sealed class UnmanagedCounterIterator<TKey,TValue>:ScanIteratorFunctions<Seamless.Collections.Empty,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    public long Counter;

    private protected override Seamless.Collections.Empty Deserialize(ref TKey key,ref TValue value) {
        this.Counter++;
        return default;
    }
}

internal sealed class UnmanagedKeyIterator<TKey,TValue>:ScanIteratorFunctions<TKey,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TKey Deserialize(ref TKey key,ref TValue value) => key;
}

internal sealed class UnmanagedValueIterator<TKey,TValue>:ScanIteratorFunctions<TValue,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TValue Deserialize(ref TKey key,ref TValue value) => value;
}

internal sealed class UnmanagedKeyValueIterator<TKey,TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override KeyValuePair<TKey,TValue> Deserialize(ref TKey key,ref TValue value) => new(key,value);
}
