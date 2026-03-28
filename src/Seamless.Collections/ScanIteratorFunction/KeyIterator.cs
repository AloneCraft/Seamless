namespace Seamless.Collections;
using MemoryPack;
internal sealed class KeyIterator<TKey>:ScanIteratorFunctions<TKey>{
    private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        MemoryPackSerializer.Deserialize<TKey>(logRecord.Key)!;
}
