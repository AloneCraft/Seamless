namespace Seamless.Collections;
using System.Collections;
using Tsavorite.core;

internal readonly struct KeyEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator>(
    ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session):IEnumerable<TKey>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TSessionFunctions:SSessionFunctions
    where TStoreFunctions:SStoreFunctions
    where TAllocator:IAllocator<SpanByte,SpanByte,TStoreFunctions>{
    public Enumerator<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator,KeyIterator<TKey>,TKey> GetEnumerator()=>new(Session,new());
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()=>this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
