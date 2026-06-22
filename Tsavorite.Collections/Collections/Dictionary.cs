namespace Seamless.Collections.Tsavorite;
/// <summary>
/// Tsavorite ???o?b?N?G???h??g???I???????? Dictionary?B
/// <see cref="IDictionary{TKey,TValue}"/> ?????????A???p??? Tsavorite ?????????????B
/// ?OI?|?????[?t?B?Y???????z?f?B?X?p?b?`?????????B
/// </summary>
[DebuggerTypeProxy(typeof(DictionaryDebugView<,,,>))]
public partial class Dictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>
    :Collection<
        TKey,
        TValue,
        TKeySerializer,
        TValueSerializer
    >,
    IDictionary<TKey,TValue>    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public bool IsReadOnly => false;
    public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        var status = this.Read(item.Key,out var output);
        if(status.NotFound)
            return false;
        return EqualityComparer<TValue>.Default.Equals(output,item.Value);
    }
    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var kvp in this) {
            array[arrayIndex++]=kvp;
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.GetEnumerator();
    public global::System.Collections.Generic.List<KeyValuePair<TKey,TValue>> ToList() {
        using var Enumerator = this.GetEnumerator();
        var capacity=checked((int)Math.Min(this.EntryCount,int.MaxValue));
        var List=new global::System.Collections.Generic.List<KeyValuePair<TKey,TValue>>(capacity);
        while(Enumerator.MoveNext()) {
            List.Add(Enumerator.Current);
        }
        return List;
    }
    public ICollection<TKey> Keys => this.CreateCollectionView<KeyIterator,TKey>();
    public ICollection<TValue> Values => this.CreateCollectionView<ValueIterator,TValue>();
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Values;
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed)
            return;
        this.disposed=true;
        if(disposing) {
            this.Remove_TKey_TValue_ClientSession.Dispose();
            this.Remove_KeyValuePair_ClientSession.Dispose();
        }
        base.Dispose(disposing);
    }
}
