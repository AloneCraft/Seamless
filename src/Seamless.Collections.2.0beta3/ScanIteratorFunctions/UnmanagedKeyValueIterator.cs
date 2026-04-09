using Seamless.Collections;
namespace Seamless.ScanIteratorFunctions;
public sealed class UnmanagedKeyValueIterator<TKey,TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        new(UnmanagedSerializer<TKey>.Deserialize(logRecord.Key),
            UnmanagedSerializer<TValue>.Deserialize(logRecord.ValueSpan));
}
