namespace Seamless.ScanIteratorFunctions;
internal sealed partial class ValueIterator<TValue,TSerializer> {
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
        TSerializer.Deserialize(logRecord.ValueSpan);
}
