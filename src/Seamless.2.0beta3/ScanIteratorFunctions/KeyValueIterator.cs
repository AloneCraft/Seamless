namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer> {
    public sealed partial class KeyValueIterator {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key=TKeySerializer.Deserialize(logRecord.Key);
            var Value=TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
}

