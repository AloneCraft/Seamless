using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;

using System.Collections;
public partial struct Enumerator<TKey, TValue,TFunctions,TStoreFunctions,TAllocator,TIterator,T>(
    ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,core_Empty,TFunctions,TStoreFunctions,TAllocator> Session,
    TIterator Iterator)
    where TKey : IEquatable<TKey>
    where TFunctions : ISessionFunctions<SpanByte, SpanByte, SpanByte, SpanByteAndMemory, core_Empty>
    where TStoreFunctions : IStoreFunctions<SpanByte, SpanByte>
    where TAllocator : IAllocator<SpanByte, SpanByte, TStoreFunctions>
    where TIterator : ScanIteratorFunctions<T>;
