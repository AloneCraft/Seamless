namespace Seamless.ScanIteratorFunctions;
using MemoryPack;
public sealed class KeyValueIterator<TKey,TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>{
    private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord){
        var Key  =MemoryPackSerializer.Deserialize<TKey>(logRecord.Key)!;
        var Value=MemoryPackSerializer.Deserialize<TValue>(logRecord.ValueSpan)!;
        return new(Key,Value);
    }
}
