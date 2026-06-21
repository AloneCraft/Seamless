using Tsavorite.Collections;

namespace Persistent.Collections;

public class Set<TRecord>(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
    : List<TRecord,MemoryPackSerializer<TRecord>>(path,initialCapacity,initialDataCapacity), Tsavorite.Collections.ISet<TRecord> {
    public Set<TRecord> Distinct() {
        this.DuplicateRemoval();
        return this;
    }

    public void Clear() {
        throw new NotImplementedException();
    }

    public bool Contains(TRecord item) {
        throw new NotImplementedException();
    }

    public void CopyTo(TRecord[] array,int arrayIndex) {
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this)
            array[arrayIndex++]=item;
    }

    public bool Remove(TRecord item) {
        throw new NotImplementedException();
    }

    public new int Count => checked((int)this.GetLongCount());
    public bool IsReadOnly => false;
    public long EntryCount => this.GetLongCount();
    IEnumerator<TRecord> Tsavorite.Collections.IMemoryCollection<TRecord>.GetEnumerator() => ((IEnumerable<TRecord>)this).GetEnumerator();
}
