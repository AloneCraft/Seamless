namespace Seamless.Tsavorite.Collections;

public sealed class DelegateSet<TRecord>:IReadOnlyCollection<TRecord>, IDisposable {
    private readonly Func<TRecord,bool> add;
    private readonly Func<TRecord,bool> remove;
    private readonly Func<TRecord,bool> contains;
    private readonly Func<IEnumerator<TRecord>> getEnumerator;
    private readonly Func<int> getCount;
    private readonly Action dispose;

    public DelegateSet(
        Func<TRecord,bool> add,
        Func<TRecord,bool> remove,
        Func<TRecord,bool> contains,
        Func<IEnumerator<TRecord>> getEnumerator,
        Func<int> getCount,
        Action? dispose = null) {
        this.add=add;
        this.remove=remove;
        this.contains=contains;
        this.getEnumerator=getEnumerator;
        this.getCount=getCount;
        this.dispose=dispose ?? (static () => { });
    }

    public int Count => this.getCount();
    public bool Add(TRecord item) => this.add(item);
    public bool Remove(TRecord item) => this.remove(item);
    public bool Contains(TRecord item) => this.contains(item);
    public IEnumerator<TRecord> GetEnumerator() => this.getEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    public void Dispose() => this.dispose();
}

public sealed class ReadSet<TRecord>:IReadOnlyCollection<TRecord>, IDisposable {
    private readonly Seamless.Persisted.Collections.SerializedList<TRecord> source;

    public ReadSet(Seamless.Persisted.Collections.SerializedList<TRecord> source) {
        this.source=source;
    }

    public int Count => checked((int)this.source.Count);
    public long LongCount => this.source.Count;
    public IEnumerator<TRecord> GetEnumerator() => this.source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    public void Dispose() => this.source.Dispose();
}
