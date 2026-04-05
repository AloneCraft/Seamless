namespace Seamless.ScanIteratorFunctions;
using MemoryPack;

using Collections;

using Tsavorite.core;
#if TSAVORITE2_0BETA3
public sealed class KeyIterator<TKey>:ScanIteratorFunctions<TKey>{
    private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        MemoryPackSerializer.Deserialize<TKey>(logRecord.Key)!;
}
#elif TSAVORITE1_1_1
public class KeyIterator<TKey>:ScanIteratorFunctions<TKey> {
    private protected override TKey Deserialize(ref SpanByte key,ref SpanByte value) =>
        MemoryPackSerializer.Deserialize<TKey>(key.AsReadOnlySpan())!;
}
#endif
