using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;

using System.Collections;
using System.Diagnostics;

using Tsavorite.core;
public struct Enumerator<TKey, TValue, TStoreFunctions, TIterator, T>(
#if TSAVORITE2_0BETA3
    ClientSession<Garnet.common.FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SessionFunctions.Add                ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Session,
#elif TSAVORITE1_1_1
    ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,SessionFunctions.Add                ,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Session,
#endif
    TIterator Iterator):IEnumerator<T>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
#if TSAVORITE2_0BETA3
    where TStoreFunctions : IStoreFunctions
#elif TSAVORITE1_1_1
    where TStoreFunctions : IStoreFunctions<SpanByte,SpanByte>
#endif
    where TIterator : ScanIteratorFunctions<T> {
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
