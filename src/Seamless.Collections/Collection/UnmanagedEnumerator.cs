#if TSAVORITE1_1_1
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
    private int Index = -1;
    private bool _done;

    public T Current => this.Iterator.Chunk[this.Index];
    object? IEnumerator.Current => this.Current;

    public bool MoveNext() {
        if(++this.Index<this.Count) {
            return true;
        } 
        if(_done) {
           this.Index = -1;
           return false;
        }
        this.Iterator.Count = 0;
        var result = session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor: false);
        var count = this.Iterator.Count;
        this.Count = count;
        if(!result) _done = true;
        if(count>0) {
            this.Index = 0;
            return true;
        }
        this.Index = -1;
        return false;
    }

    public void Reset() {
        this.Cursor = 0;
       this.Count = 0;
        this.Index = -1;
       _done = false;
    }

    public void Dispose() {
    }
}
#endif
