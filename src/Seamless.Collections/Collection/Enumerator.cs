namespace Seamless.Collections;

using System.Collections;
public partial struct Enumerator<TKey, TValue,TFunctions,TStoreFunctions,TAllocator,TIterator,T>:IEnumerator<T> {
    private TIterator Iterator = Iterator;
    private long Cursor = 0, Count;
    private int Index = -1;
    private bool _done;
    public T Current => this.Iterator.Chunk[this.Index];
    object? IEnumerator.Current => this.Current;
    public bool MoveNext() {
        if(++this.Index<this.Count) return true;
        if(this._done) {
           this.Index = -1;
           return false;
        }
        this.Iterator.Count = 0;
        var r = Session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor: false);
        var Count = this.Iterator.Count;
        this.Count=Count;
        if(!r)
            this._done = true;
        if(Count>0) {
            this.Index=0;
            return true;
        }
        this.Index=-1;
        return false;
    }
    public void Reset() {
        this.Cursor=0;
       this.Count=0;
        this.Index=-1;
        this._done = false;
    }
    public void Dispose() {
    }
}
