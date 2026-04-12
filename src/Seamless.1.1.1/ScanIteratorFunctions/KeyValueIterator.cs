namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer> {
    public sealed partial class KeyValueIterator {
        private protected override KeyValuePair<TKey,TValue> Deserialize(ref SpanByte key,ref SpanByte value) {
            var Key=TKeySerializer.Deserialize(key.AsReadOnlySpan());
            var Value=TValueSerializer.Deserialize(value.AsReadOnlySpan());
            return new(Key,Value);
        }
    }
}

