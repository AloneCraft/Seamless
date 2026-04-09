using Seamless.Collections;
namespace Seamless.ScanIteratorFunctions;
internal sealed class UnmanagedValueIterator<TKey,TValue>:ScanIteratorFunctions<TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        UnmanagedSerializer<TValue>.Deserialize(logRecord.ValueSpan);
}
