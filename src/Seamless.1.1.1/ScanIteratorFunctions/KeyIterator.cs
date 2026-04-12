using System.Runtime.CompilerServices;
namespace Seamless.ScanIteratorFunctions;
public sealed partial class KeyIterator<T,TSerializer> {
    private protected override T Deserialize(ref SpanByte key,ref SpanByte value) =>
        TSerializer.Deserialize(key.AsReadOnlySpan());
}
//public sealed partial class KeyIterator<T,TKey,TValue> {
//    private protected override T Deserialize(ref TKey key,ref TValue value) =>
//        Unsafe.As<TKey, T>(ref key);
//}
