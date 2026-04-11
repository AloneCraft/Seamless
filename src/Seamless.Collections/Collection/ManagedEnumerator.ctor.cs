#if !(TSAVORITE2_0BETA3 || TSAVORITE1_1_1)
using Seamless.ScanIteratorFunctions;
using Tsavorite.core;
using Garnet.common;
namespace Seamless.Collections;
public partial struct ManagedEnumerator<TKey, TValue,TReadFunctions,TStoreFunctions,TAllocator,TIterator,T>(
    ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,EqualityEmpty,TReadFunctions,TStoreFunctions,TAllocator> Session,
    TIterator Iterator)
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TReadFunctions : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, EqualityEmpty>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions>
    where TIterator : ScanIteratorFunctions<T>;
#endif
