namespace Seamless.Tsavorite.Collections;

public sealed class DelegateSet<TRecord>(
    Func<TRecord,bool>         add,
    Func<TRecord,bool>         remove,
    Func<TRecord,bool>         contains,
    Func<IEnumerator<TRecord>> getEnumerator,
    Func<int>                  getCount,
    Action?                    dispose=null)
    :IReadOnlyCollection<TRecord>,IDisposable {
    private readonly Action                     dispose      =dispose??(static () => { });
    public           int                        Count                  => getCount();
    public           bool                       Add(TRecord      item) => add(item);
    public           bool                       Remove(TRecord   item) => remove(item);
    public           bool                       Contains(TRecord item) => contains(item);
    public           IEnumerator<TRecord>       GetEnumerator()        => getEnumerator();
    IEnumerator IEnumerable.                    GetEnumerator()        => this.GetEnumerator();
    public void                                 Dispose()              => this.dispose();
}

public sealed class ReadSet<TRecord>(SerializedList<TRecord> source):IReadOnlyCollection<TRecord>,IDisposable {
    public int Count => checked((int)source.Count);
    public long LongCount => source.Count;
    public IEnumerator<TRecord> GetEnumerator() => source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    public void Dispose() => source.Dispose();
}
