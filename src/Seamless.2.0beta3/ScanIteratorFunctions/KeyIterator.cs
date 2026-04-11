namespace Seamless.ScanIteratorFunctions;
public sealed partial class KeyIterator<T,TSerializer> {
    private protected override T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        TSerializer.Deserialize(logRecord.Key);
}
public sealed partial class KeyIterator<T,TKey,TValue> {
    private protected override T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        MemoryPack.MemoryPackSerializer.Deserialize<T>(logRecord.Key)!;
}
