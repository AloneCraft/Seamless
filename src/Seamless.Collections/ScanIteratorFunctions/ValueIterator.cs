namespace Seamless.ScanIteratorFunctions;

using MemoryPack;
using Tsavorite.core;
internal sealed class ValueIterator<TValue>:ScanIteratorFunctions<TValue> {
#if TSAVORITE2_0BETA3
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
        MemoryPackSerializer.Deserialize<TValue>(logRecord.ValueSpan)!;
#elif TSAVORITE1_1_1
    private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value) =>
        MemoryPackSerializer.Deserialize<TValue>(value.AsReadOnlySpan())!;
#endif
}
