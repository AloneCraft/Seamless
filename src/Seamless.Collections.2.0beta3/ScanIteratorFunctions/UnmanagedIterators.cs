using Seamless.Collections;
namespace Seamless.ScanIteratorFunctions;

internal sealed class UnmanagedKeyIterator<TKey,TValue>:ScanIteratorFunctions<TKey>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        UnmanagedSerializer<TKey>.Deserialize(logRecord.Key);
}

internal sealed class UnmanagedValueIterator<TKey,TValue>:ScanIteratorFunctions<TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        UnmanagedSerializer<TValue>.Deserialize(logRecord.ValueSpan);
}

public sealed class UnmanagedKeyValueIterator<TKey,TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        new(UnmanagedSerializer<TKey>.Deserialize(logRecord.Key),
            UnmanagedSerializer<TValue>.Deserialize(logRecord.ValueSpan));
}
