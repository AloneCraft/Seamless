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

    public int Count => getCount();
    public bool Add(TRecord item) => add(item);
    public bool Remove(TRecord item) => remove(item);
    public bool Contains(TRecord item) => contains(item);
    public IEnumerator<TRecord> GetEnumerator() => getEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Dispose() => dispose();
}

public sealed class ReadSet<TRecord>:IReadOnlyCollection<TRecord>, IDisposable {
    private readonly SerializedList<TRecord> source;

    public ReadSet(SerializedList<TRecord> source) {
        this.source=source;
    }

    public int Count => checked((int)source.Count);
    public long LongCount => source.Count;
    public IEnumerator<TRecord> GetEnumerator() => source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Dispose() => source.Dispose();
}
