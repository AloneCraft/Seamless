namespace Seamless.Database;

/// <summary>
/// 一度実体化された読み取り専用の関係。行を直接列挙でき、候補キーで探索できる。
/// 内容の構築と寿命はDatabaseContainerが管理する。
/// </summary>
public sealed class MaterializedView<TKey,TRecord>:IReadOnlyCollection<TRecord>,IDisposable
    where TKey:struct
    where TRecord:struct {
    private readonly DatabaseContainer database;
    private readonly global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> rows;
    private readonly Func<TRecord,TKey> keySelector;
    private readonly IDisposable ownedResources;
    private long count;
    private bool disposed;

    internal MaterializedView(
        DatabaseContainer database,
        global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> rows,
        Func<TRecord,TKey> keySelector,
        IDisposable ownedResources,
        string name) {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rows);
        this.database=database;
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(ownedResources);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this.rows=rows;
        this.keySelector=keySelector;
        this.ownedResources=ownedResources;
        this.Name=name;
        this.count=rows.GetLongCount();
    }

    /// <summary>宣言メンバー名。</summary>
    public string Name { get; }
    public long LongCount {
        get {
            var overlay=this.database.GetTransactionOverlay(this,create:false);
            if(overlay is not null){return overlay.LongCount;}
            return this.database.WithReadGate(()=>this.count);
        }
    }
    public int Count {
        get=>checked((int)this.LongCount);
    }
    public IEnumerable<TKey> Keys {
        get {
            var overlay=this.database.GetTransactionOverlay(this,create:false);
            if(overlay is not null){return overlay.Keys;}
            return this.EnumerateKeys();
        }
    }
    public TRecord this[TKey key]=>this.TryGetValue(key,out var record)?record:throw new KeyNotFoundException();

    public bool ContainsKey(TKey key)=>this.TryGetValue(key,out _);

    public bool TryGetValue(TKey key,[global::System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out TRecord record) {
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null){return overlay.TryGetValue(key,out record);}
        TRecord found=default;
        var result=this.database.WithReadGate(()=>this.rows.TryGetValue(key,out found));
        record=found;
        return result;
    }

    internal bool Add(TRecord record) {
        var key=this.keySelector(record);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is not null) {
            return overlay.Add(key,record);
        }
        return this.database.WithWriteGate(()=>this.DirectAdd(key,record));
    }

    internal bool DirectAdd(TKey key,TRecord record) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        switch(this.rows.TryAdd(key,record)) {
            case global::Seamless.Collections.Tsavorite.TryAddResult.KeyAlreadyExists:
                return false;
            case global::Seamless.Collections.Tsavorite.TryAddResult.LogFull:
                throw new OutOfMemoryException($"Materialized view '{this.Name}' storage is full.");
        }
        this.count++;
        return true;
    }

    internal void DirectSet(TKey key,TRecord record) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.rows.ContainsKey(key)) {
            this.rows[key]=record;
            return;
        }
        this.rows[key]=record;
        this.count++;
    }

    internal bool DirectTryGetValue(TKey key,out TRecord record)=>this.rows.TryGetValue(key,out record);
    internal long DirectLongCount=>this.count;
    internal IEnumerable<KeyValuePair<TKey,TRecord>> DirectRows=>this.rows;
    internal TKey DirectKey(TRecord record)=>this.keySelector(record);

    public IEnumerator<TRecord> GetEnumerator() {
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null){return overlay.Rows.GetEnumerator();}
        return this.database.Stream(()=>this.rows.Values.GetEnumerator());
    }
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();

    private IEnumerable<TKey> EnumerateKeys() {
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
