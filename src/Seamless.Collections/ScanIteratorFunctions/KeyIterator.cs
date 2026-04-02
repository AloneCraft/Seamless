namespace Seamless.ScanIteratorFunctions;
using MemoryPack;
public sealed class KeyIterator<TKey>:ScanIteratorFunctions<TKey>{
    private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        MemoryPackSerializer.Deserialize<TKey>(logRecord.Key)!;
}
