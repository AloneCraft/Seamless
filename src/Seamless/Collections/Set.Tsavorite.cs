using System.Collections;

namespace Seamless.Collections;

public partial class Set<TKey, TRecord>
    where TRecord : IRecord<TKey> {
    public IEnumerator<TRecord> GetEnumerator() => new Enumerator<ValueIterator,TRecord>(this.ReadSession,new());
    IEnumerator<TRecord> IEnumerable<TRecord>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TRecord> IMemoryCollection<TRecord>.GetEnumerator() => new Enumerator<MemoryValueIterator,TRecord>(this.ReadSession,new());
}
