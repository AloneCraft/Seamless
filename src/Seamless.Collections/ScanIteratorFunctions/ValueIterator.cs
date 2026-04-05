namespace Seamless.ScanIteratorFunctions;

using MemoryPack;

using Collections;

using Tsavorite.core;
#if TSAVORITE2_0BETA3
internal sealed class ValueIterator<TValue>:ScanIteratorFunctions<TValue> {
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
        MemoryPackSerializer.Deserialize<TValue>(logRecord.ValueSpan)!;
}
#elif TSAVORITE1_1_1
public sealed class ValueIterator<TValue>:ScanIteratorFunctions<TValue> {
    private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value) =>
        MemoryPackSerializer.Deserialize<TValue>(value.AsReadOnlySpan())!;
}
#endif
