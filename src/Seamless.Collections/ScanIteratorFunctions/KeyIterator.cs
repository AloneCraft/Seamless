using System.Runtime.CompilerServices;
namespace Seamless.ScanIteratorFunctions;
using Seamless.Collections;
using Tsavorite.core;
public sealed class KeyIterator<T,TSerializer>:ScanIteratorFunctions<T>
    where TSerializer:ISerializer<T>{
#if TSAVORITE2_0BETA3
    private protected override T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        TSerializer.Deserialize(logRecord.Key);
#elif TSAVORITE1_1_1
    private protected override T Deserialize(ref SpanByte key,ref SpanByte value) =>
        TSerializer.Deserialize(key.AsReadOnlySpan());
#endif
}
public sealed class KeyIterator<T,TKey,TValue>:ScanIteratorFunctions<T,TKey,TValue>{
#if TSAVORITE2_0BETA3
    private protected override T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
        MemoryPack.MemoryPackSerializer.Deserialize<T>(logRecord.Key)!;
#elif TSAVORITE1_1_1
    private protected override T Deserialize(ref TKey key,ref TValue value) =>
        Unsafe.As<TKey, T>(ref key);
#endif
}
