namespace Seamless.ScanIteratorFunctions;
/// <summary>
/// キーをデシリアライズせず byte[] として返すイテレータ。IntersectWith/SymmetricExceptWith の最適化に使用。
/// </summary>
public sealed class SpanByteKeyIterator:ScanIteratorFunctions<byte[]>{
    private protected override byte[] Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        logRecord.Key.ToArray();
}
