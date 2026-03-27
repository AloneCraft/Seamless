namespace Seamless.Collections;
using System.Collections;
using Tsavorite.core;
internal readonly struct ValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator>(ClientSession<Garnet.common.FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session):IEnumerable<TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TSessionFunctions:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
    where TStoreFunctions:IStoreFunctions
    where TAllocator:IAllocator<TStoreFunctions>{
    public Enumerator<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator,ValueIterator<TValue>,TValue> GetEnumerator()=>new(Session,new());
    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()=>this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
