using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;

using System.Collections;
using System.Diagnostics;

using Tsavorite.core;
using core_Empty=Tsavorite.core.Empty;
public struct Enumerator<TKey, TValue,TFunctions,TStoreFunctions,TAllocator,TIterator,T>(
#if TSAVORITE2_0BETA3
    ClientSession<TKey,TValue,core_Empty,T,core_Empty,TFunctions,TStoreFunctions,TAllocator> Session,
#elif TSAVORITE1_1_1
    ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,core_Empty,TFunctions,TStoreFunctions,TAllocator> Session,
#endif
    TIterator Iterator):IEnumerator<T>
    where TKey : IEquatable<TKey>
#if TSAVORITE2_0BETA3
    where TValue : IEquatable<TValue>
    where TStoreFunctions : IStoreFunctions
    where TIterator : ScanIteratorFunctions<T,TKey,TValue> {
#elif TSAVORITE1_1_1
    where TFunctions : ISessionFunctions<SpanByte, SpanByte, SpanByte, SpanByteAndMemory, core_Empty>
    where TStoreFunctions : IStoreFunctions<SpanByte, SpanByte>
    where TAllocator : IAllocator<SpanByte, SpanByte, TStoreFunctions>
    where TIterator : ScanIteratorFunctions<T> {
#endif
    private TIterator Iterator = Iterator;
    private long Cursor = 0, Count;
    private int Index = Const.Chunk_Length-1;
    public T Current => this.Iterator.Chunk[this.Index];
    object? IEnumerator.Current => this.Current;
    public bool MoveNext() {
        if(++this.Index<this.Count) return true;
        var r = Session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor: false);
        Debug.Assert(!r);
        var Count = this.Iterator.Count;
        this.Count=Count;
        if(Count>0) {
            this.Index=0;
            return true;
        }
        this.Index=-1;
        return false;
    }
    public void Reset() {
        this.Cursor=0;
        this.Index=Const.Chunk_Length-1;
    }
    public void Dispose() {
    }
}
