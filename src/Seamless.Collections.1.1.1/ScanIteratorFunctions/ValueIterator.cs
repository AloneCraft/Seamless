namespace Seamless.ScanIteratorFunctions;
internal sealed partial class ValueIterator<TValue,TSerializer> {
    private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value) =>
        TSerializer.Deserialize(value.AsReadOnlySpan());
}
