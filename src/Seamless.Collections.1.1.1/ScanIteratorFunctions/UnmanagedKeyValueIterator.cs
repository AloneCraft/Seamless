using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;
internal sealed class UnmanagedKeyValueIterator<TKey,TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override KeyValuePair<TKey,TValue> Deserialize(ref TKey key,ref TValue value) => new(key,value);
}
