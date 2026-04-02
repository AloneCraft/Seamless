namespace Seamless.ScanIteratorFunctions;

using MemoryPack;
internal sealed class ValueIterator<TValue>:ScanIteratorFunctions<TValue> {
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
        MemoryPackSerializer.Deserialize<TValue>(logRecord.ValueSpan)!;
}
