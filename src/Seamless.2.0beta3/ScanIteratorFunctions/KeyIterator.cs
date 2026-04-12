namespace Seamless.ScanIteratorFunctions;
public sealed partial class KeyIterator<T,TSerializer> {
    private protected override T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        TSerializer.Deserialize(logRecord.Key);
}