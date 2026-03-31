namespace Seamless.Collections.Enumerators;
using System.Collections;
using Tsavorite.core;
internal readonly struct KeyValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Collections.Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session):IEnumerable<KeyValuePair<TKey,TValue>>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TSessionFunctions:Tsavorite.core.ISessionFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteAndMemory,Seamless.Collections.Empty>
    where TStoreFunctions:Tsavorite.core.IStoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte>
    where TAllocator:IAllocator<SpanByte,SpanByte,TStoreFunctions>{
    public Enumerator<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator,KeyValueIterator<TKey,TValue>,KeyValuePair<TKey,TValue>> GetEnumerator()=>new(Session,new());
    IEnumerator<KeyValuePair<TKey,TValue>>IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator()=>this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
