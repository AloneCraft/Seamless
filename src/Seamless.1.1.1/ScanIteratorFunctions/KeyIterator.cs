namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer> {
    public sealed partial class KeyIterator {
        private protected override TKey Deserialize(ref SpanByte key,ref SpanByte value) =>
            TKeySerializer.Deserialize(key.AsReadOnlySpan());
    }
}

