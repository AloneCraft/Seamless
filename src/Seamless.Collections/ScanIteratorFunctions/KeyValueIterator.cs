namespace Seamless.ScanIteratorFunctions;
using MemoryPack;

using Collections;

using Tsavorite.core;
#if TSAVORITE2_0BETA3
public sealed class KeyValueIterator<TKey,TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>{
    private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord){
        var Key  =MemoryPackSerializer.Deserialize<TKey>(logRecord.Key)!;
        var Value=MemoryPackSerializer.Deserialize<TValue>(logRecord.ValueSpan)!;
        return new(Key,Value);
    }
}
#elif TSAVORITE1_1_1
public class KeyValueIterator<TKey, TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>> {
    private protected override KeyValuePair<TKey,TValue> Deserialize(ref SpanByte key,ref SpanByte value) {
        var Key = MemoryPackSerializer.Deserialize<TKey>(key.AsReadOnlySpan())!;
        var Value = MemoryPackSerializer.Deserialize<TValue>(value.AsReadOnlySpan())!;
        return new KeyValuePair<TKey,TValue>(Key,Value);
    }
}
#endif
