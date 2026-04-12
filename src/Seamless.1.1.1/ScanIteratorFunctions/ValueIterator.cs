namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer> {
    internal sealed partial class ValueIterator {
        private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value) =>
            TValueSerializer.Deserialize(value.AsReadOnlySpan());
    }
}

