namespace Seamless.ScanIteratorFunctions;
public sealed partial class KeyValueIterator<TKey,TValue,TKeySerializer,TValueSerializer> {
    private protected override KeyValuePair<TKey,TValue> Deserialize(ref SpanByte key,ref SpanByte value) {
        var Key = TKeySerializer.Deserialize(key.AsReadOnlySpan());
        var Value = TValueSerializer.Deserialize(value.AsReadOnlySpan());
        return new(Key,Value);
    }
}
