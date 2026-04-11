#if !(TSAVORITE2_0BETA3 || TSAVORITE1_1_1)
using Seamless.ScanIteratorFunctions;
using Tsavorite.core;
using Garnet.common;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer> {
    public partial struct Enumerator<TIterator,T>(
        ClientSession<FixedSpanByteKey,EqualityEmpty,SpanByteAndMemory,EqualityEmpty,ManagedRead,ManagedStoreFunctions,ManagedAllocator> Session,
        TIterator Iterator)
        where TIterator : ScanIteratorFunctions<T>;
}
#endif
