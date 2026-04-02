namespace Seamless.Collections;

using MemoryPack;
using Tsavorite.core;
public class KeyIterator<TKey>:ScanIteratorFunctions<TKey> {
    private protected override TKey Deserialize(ref SpanByte key,ref SpanByte value) =>
        MemoryPackSerializer.Deserialize<TKey>(key.AsReadOnlySpan())!;
}
