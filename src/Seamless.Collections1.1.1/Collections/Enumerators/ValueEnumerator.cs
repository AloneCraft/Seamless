namespace Seamless.Collections.Enumerators;
using System.Collections;
using Tsavorite.core;
internal readonly struct ValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Collections.Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session):IEnumerable<TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TSessionFunctions:Tsavorite.core.ISessionFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteAndMemory,Seamless.Collections.Empty>
    where TStoreFunctions:Tsavorite.core.IStoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte>
    where TAllocator:IAllocator<SpanByte,SpanByte,TStoreFunctions>{
    public Enumerator<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator,ValueIterator<TValue>,TValue> GetEnumerator()=>new(Session,new());
    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()=>this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
