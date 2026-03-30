using Common;
namespace Seamless.Collections;

using System.Collections;
using System.Diagnostics;

using Tsavorite.core;
internal struct Enumerator<TKey, TValue, TSessionFunctions, TStoreFunctions0, TAllocator0, TIterator, T>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions0,TAllocator0> Session,TIterator Iterator):IEnumerator<T>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TSessionFunctions : SSessionFunctions
    where TStoreFunctions0 : SStoreFunctions
    where TAllocator0 : IAllocator<SpanByte,SpanByte,TStoreFunctions0>
    where TIterator : ScanIteratorFunctions<T> {
    private TIterator Iterator = Iterator;
    private long Cursor = 0, Count;
    private int Index = Const.Chunk_Length-1;
    private bool IsDisposed;
    public readonly T Current => this.Iterator.Chunk[this.Index];
    readonly object? IEnumerator.Current => this.Current;
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
        if(this.IsDisposed) return;
        this.IsDisposed=true;
        Session.BasicContext.CompletePending(wait: true);
    }
}
