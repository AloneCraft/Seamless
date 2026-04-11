using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer> {
    public partial struct Enumerator<TIterator,T>(
        ClientSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            ManagedRead,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        TIterator Iterator)
        where TIterator : ScanIteratorFunctions<T>;
}
