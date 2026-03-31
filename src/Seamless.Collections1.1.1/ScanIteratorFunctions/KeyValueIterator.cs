namespace Seamless.Collections;

using MemoryPack;
using Tsavorite.core;
public class KeyValueIterator<TKey, TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>> {
    private protected override KeyValuePair<TKey,TValue> Deserialize(ref SpanByte key,ref SpanByte value) {
        var Key = MemoryPackSerializer.Deserialize<TKey>(key.AsReadOnlySpan())!;
        var Value = MemoryPackSerializer.Deserialize<TValue>(value.AsReadOnlySpan())!;
        return new KeyValuePair<TKey,TValue>(Key,Value);
    }
}
