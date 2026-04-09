using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;
internal sealed class UnmanagedValueIterator<TKey,TValue>:ScanIteratorFunctions<TValue,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TValue Deserialize(ref TKey key,ref TValue value) => value;
}
