using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;
public partial struct Enumerator<TKey, TValue,TFunctions,TStoreFunctions,TAllocator,TIterator,T>(
    ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,TFunctions,TStoreFunctions,TAllocator> Session,
    TIterator Iterator)
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TFunctions : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, core_Empty>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions>
    where TIterator : ScanIteratorFunctions<T>;
