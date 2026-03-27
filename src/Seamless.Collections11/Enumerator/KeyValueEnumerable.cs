namespace Seamless.Collections;
using System.Collections;
using Tsavorite.core;
internal readonly struct KeyValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session):IEnumerable<KeyValuePair<TKey,TValue>>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TSessionFunctions:ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>
    where TStoreFunctions:IStoreFunctions<SpanByte,SpanByte>
    where TAllocator:IAllocator<SpanByte,SpanByte,TStoreFunctions>{
    public Enumerator<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator,KeyValueIterator<TKey,TValue>,KeyValuePair<TKey,TValue>> GetEnumerator()=>new(Session,new());
    IEnumerator<KeyValuePair<TKey,TValue>>IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator()=>this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
