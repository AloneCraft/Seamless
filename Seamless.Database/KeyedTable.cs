namespace Seamless.Database;

/// <summary>候補キーで探索できる物理テーブル。</summary>
public sealed class Table<TKey,TRecord>:global::System.Collections.Generic.IReadOnlyCollection<TRecord>,IDisposable
    where TKey:struct
    where TRecord:struct {
    private readonly DatabaseContainer database;
    private readonly global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> rows;
    private readonly Func<TRecord,TKey> keySelector;
    private readonly IDisposable ownedResources;
    private bool disposed;

    internal Table(
        DatabaseContainer database,
        global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> rows,
        Func<TRecord,TKey> keySelector,
        IDisposable ownedResources,
        string name) {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(ownedResources);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this.database=database;
        this.rows=rows;
        this.keySelector=keySelector;
        this.ownedResources=ownedResources;
        this.Name=name;
    }

    public string Name { get; }
    public long LongCount {
        get {
            var overlay=this.database.GetTransactionOverlay(this,create:false);
            if(overlay is not null){return overlay.LongCount;}
            return this.database.WithReadGate(()=>this.rows.GetLongCount());
        }
    }
    public int Count {
        get=>checked((int)this.LongCount);
    }
    public IEnumerable<TKey> Keys=>this.EnumerateKeys();
    public TRecord this[TKey key] {
        get=>this.TryGetValue(key,out var record)?record:throw new KeyNotFoundException();
        set {
            ObjectDisposedException.ThrowIf(this.disposed,this);
            var overlay=this.database.GetTransactionOverlay(this,create:true);
            if(overlay is not null) {
                overlay.Set(key,value);
                return;
            }
            this.database.WithWriteGate(()=> {
                this.DirectSet(key,value);
                return true;
            });
        }
    }

    public bool ContainsKey(TKey key)=>this.TryGetValue(key,out _);
    public bool TryGetValue(TKey key,[global::System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out TRecord record) {
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null) {
            return overlay.TryGetValue(key,out record);
        }
        TRecord found=default;
        var result=this.database.WithReadGate(()=>this.DirectTryGetValue(key,out found));
        record=found;
        return result;
    }

    public bool Add(TRecord record) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var key=this.keySelector(record);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        return overlay is not null
            ?overlay.Add(key,record)
            :this.database.WithWriteGate(()=>this.DirectAdd(key,record));
    }

    public bool Remove(TKey key) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        return overlay is not null
            ?overlay.Remove(key)
            :this.database.WithWriteGate(()=>this.DirectRemove(key));
    }

    internal bool DirectAdd(TKey key,TRecord record)=>
        this.rows.TryAdd(key,record) switch {
            global::Seamless.Collections.Tsavorite.TryAddResult.Success=>true,
            global::Seamless.Collections.Tsavorite.TryAddResult.KeyAlreadyExists=>false,
            global::Seamless.Collections.Tsavorite.TryAddResult.LogFull=>
                throw new OutOfMemoryException($"Table '{this.Name}' storage is full."),
            _=>throw new InvalidOperationException(),
        };

    internal void DirectSet(TKey key,TRecord record)=>this.rows[key]=record;
    internal bool DirectRemove(TKey key)=>this.rows.Remove(key);
    internal bool DirectTryGetValue(TKey key,out TRecord record)=>this.rows.TryGetValue(key,out record);
    internal long DirectLongCount=>this.rows.GetLongCount();
    internal IEnumerable<KeyValuePair<TKey,TRecord>> DirectRows=>this.rows;
    internal TKey DirectKey(TRecord record)=>this.keySelector(record);

    public IEnumerator<TRecord> GetEnumerator() {
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null){return overlay.Rows.GetEnumerator();}
        return this.database.Stream(()=>this.rows.Values.GetEnumerator());
    }
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();

    private IEnumerable<TKey> EnumerateKeys() {
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null){
            foreach(var key in overlay.Keys){yield return key;}
            yield break;
        }
        using var iterator=this.database.Stream(()=>this.rows.Keys.GetEnumerator());
        while(iterator.MoveNext()){yield return iterator.Current;}
    }

    public void Dispose() {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        this.ownedResources.Dispose();
    }
}
