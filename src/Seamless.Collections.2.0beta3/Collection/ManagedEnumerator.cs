using System.Runtime.InteropServices;
using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;
public partial struct ManagedEnumerator<TKey, TValue,TReadFunctions,TStoreFunctions,TAllocator,TIterator,T>(
    ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        TReadFunctions,
        TStoreFunctions,
        TAllocator
    > Session,
    TIterator Iterator)
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TReadFunctions : ISessionFunctions<EqualityEmpty, SpanByteAndMemory, EqualityEmpty>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions>
    where TIterator : ScanIteratorFunctions<T>;
