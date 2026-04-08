namespace Seamless.ScanIteratorFunctions;
public sealed partial class KeyValueIterator<TKey,TValue,TKeySerializer,TValueSerializer> {
    private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord){
        var Key  =TKeySerializer.Deserialize(logRecord.Key);
        var Value=TValueSerializer.Deserialize(logRecord.ValueSpan);
        return new(Key,Value);
    }
}
