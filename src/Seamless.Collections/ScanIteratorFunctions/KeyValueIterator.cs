namespace Seamless.ScanIteratorFunctions;
using Seamless.Collections;
using Tsavorite.core;
public sealed class KeyValueIterator<TKey,TValue,TKeySerializer,TValueSerializer>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
#if TSAVORITE2_0BETA3
    private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord){
        var Key  =TKeySerializer.Deserialize(logRecord.Key);
        var Value=TValueSerializer.Deserialize(logRecord.ValueSpan);
        return new(Key,Value);
    }
#elif TSAVORITE1_1_1
    private protected override KeyValuePair<TKey,TValue> Deserialize(ref SpanByte key,ref SpanByte value) {
        var Key = TKeySerializer.Deserialize(key.AsReadOnlySpan());
        var Value = TValueSerializer.Deserialize(value.AsReadOnlySpan());
        return new(Key,Value);
    }
#endif
}
