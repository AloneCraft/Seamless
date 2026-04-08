using Seamless.ScanIteratorFunctions;

using System.Collections;
using System.Diagnostics;

using Tsavorite.core;

namespace Seamless.Collections;
using core_Empty=Tsavorite.core.Empty;

internal struct UnmanagedEnumerator<TKey,TValue,TIterator,T>(
    ClientSession<
        TKey,
        TValue,
        TValue,
        TValue,
        core_Empty,
        Seamless.SessionFunctions.UnmanagedAdd<TKey,TValue>,
        StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>,
        BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>> session,
    TIterator iterator):IEnumerator<T>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TIterator:ScanIteratorFunctions<T,TKey,TValue> {

    private TIterator Iterator = iterator;
    private long Cursor = 0, Count;
    private int Index = Const.Chunk_Length-1;

    public T Current => this.Iterator.Chunk[this.Index];
    object? IEnumerator.Current => this.Current;

    public bool MoveNext() {
        if(++this.Index<this.Count) {
            return true;
        }
        var result = session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor: false);
        Debug.Assert(!result);
        var count = this.Iterator.Count;
        this.Count = count;
        if(count>0) {
            this.Index = 0;
            return true;
        }
        this.Index = -1;
        return false;
    }

    public void Reset() {
        this.Cursor = 0;
        this.Index = Const.Chunk_Length-1;
    }

    public void Dispose() {
    }
}
