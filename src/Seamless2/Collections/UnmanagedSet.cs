using System.Collections;

namespace Seamless.Collections;

public partial class UnmanagedSet<TRecord, TKey>
    where TRecord : unmanaged, IRecord<TKey>
    where TKey : unmanaged {
    public UnmanagedList<TRecord>.Enumerator GetEnumerator() {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-set-enumerator");
        try {
            var items = ListStore.Create<TRecord>(workListPath,initialCapacity: Const.Chunk_Length);
            var iterator = new ValueIterator();
            iterator.SetList(items);
            var cursor = 0L;
            this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            return new(items,ownsList: true,disposeAction: () => DeleteTemporaryWorkListPath(workListPath));
        }
        catch {
            DeleteTemporaryWorkListPath(workListPath);
            throw;
        }
    }
    IEnumerator<TRecord> IEnumerable<TRecord>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TRecord> IMemoryCollection<TRecord>.GetEnumerator() {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-set-memory-enumerator");
        try {
            var items = ListStore.Create<TRecord>(workListPath,initialCapacity: Const.Chunk_Length);
            var iterator = new MemoryValueIterator();
            iterator.SetList(items);
            var cursor = 0L;
            this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            return new UnmanagedList<TRecord>.Enumerator(items,ownsList: true,disposeAction: () => DeleteTemporaryWorkListPath(workListPath));
        }
        catch {
            DeleteTemporaryWorkListPath(workListPath);
            throw;
        }
    }
}
