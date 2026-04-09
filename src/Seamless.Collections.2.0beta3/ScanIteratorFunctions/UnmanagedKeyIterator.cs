using Seamless.Collections;
namespace Seamless.ScanIteratorFunctions;

internal sealed class UnmanagedKeyIterator<TKey,TValue>:ScanIteratorFunctions<TKey>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        UnmanagedSerializer<TKey>.Deserialize(logRecord.Key);
}
