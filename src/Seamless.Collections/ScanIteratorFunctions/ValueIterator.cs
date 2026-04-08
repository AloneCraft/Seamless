namespace Seamless.ScanIteratorFunctions;

using Seamless.Collections;
using Tsavorite.core;
internal sealed class ValueIterator<TValue,TSerializer>:ScanIteratorFunctions<TValue>
    where TSerializer:ISerializer<TValue> {
#if TSAVORITE2_0BETA3
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
        TSerializer.Deserialize(logRecord.ValueSpan);
#elif TSAVORITE1_1_1
    private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value) =>
        TSerializer.Deserialize(value.AsReadOnlySpan());
#endif
}
