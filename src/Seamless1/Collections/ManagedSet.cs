using System.Collections;

namespace Seamless.Collections;

public partial class ManagedSet<TRecord, TKey>
    where TRecord : IRecord<TKey> {
    public ManagedList<TRecord>.Enumerator GetEnumerator() {
        var items = new InMemoryList<TRecord>(Const.Chunk_Length);
        var iterator = new ValueIterator();
        iterator.SetList(items);
        this.IterateLookupAll(this.ReadSession,ref iterator);
        return new ManagedList<TRecord>.Enumerator(items,ownsList: true);
    }
    IEnumerator<TRecord> IEnumerable<TRecord>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TRecord> IMemoryCollection<TRecord>.GetEnumerator() {
        var items = new InMemoryList<TRecord>(Const.Chunk_Length);
        var iterator = new ValueIterator();
        iterator.SetList(items);
        this.IterateLookupAll(this.ReadMemoryClientSession,ref iterator);
        return new ManagedList<TRecord>.Enumerator(items,ownsList: true);
    }
}
