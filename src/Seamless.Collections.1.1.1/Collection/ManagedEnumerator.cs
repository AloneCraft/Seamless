using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;
public partial struct ManagedEnumerator<TKey, TValue,TReadFunctions,TStoreFunctions,TAllocator,TIterator,T>(
    ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        EqualityEmpty,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        TReadFunctions,
        TStoreFunctions,
        TAllocator
    > Session,
    TIterator Iterator)
    where TKey : IEquatable<TKey>
    where TReadFunctions : ISessionFunctions<SpanByte, SpanByte, EqualityEmpty, SpanByteAndMemory, EqualityEmpty>
    where TStoreFunctions : IStoreFunctions<SpanByte, SpanByte>
    where TAllocator : IAllocator<SpanByte, SpanByte, TStoreFunctions>
    where TIterator : ScanIteratorFunctions<T>;
//public readonly partial struct ManagedRead():
//    ISessionFunctions<SpanByte,SpanByte,EqualityEmpty,SpanByteAndMemory,EqualityEmpty>{
