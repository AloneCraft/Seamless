using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

/// <summary>
/// 一度実体化された読み取り専用の関係。行を直接列挙でき、候補キーで探索できる。
/// 内容の構築と寿命はDatabaseContainerが管理する。
/// </summary>
public sealed class MaterializedView<TKey,TRecord>:IReadOnlyCollection<TRecord>,IDisposable
    where TKey:struct
    where TRecord:struct {
    private readonly DatabaseContainer database;
    private global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>? legacyRows;
    private global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> rows{get=>this.legacyRows??throw new InvalidOperationException("Use the generated versioned transaction collection.");set=>this.legacyRows=value;}
    private readonly Func<global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>>? sharedFactory;
    private readonly Func<TRecord,TKey> keySelector;
    private readonly IDisposable ownedResources;
    private long count;
    private bool disposed;
    internal MaterializedView(DatabaseContainer database,Func<TRecord,TKey> keySelector,IDisposable ownedResources,string name){this.database=database;this.keySelector=keySelector;this.ownedResources=ownedResources;this.Name=name;}

    internal MaterializedView(
        DatabaseContainer database,
        global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> rows,
        Func<TRecord,TKey> keySelector,
        IDisposable ownedResources,
        string name,Func<global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>>? sharedFactory=null) {
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
        this.sharedFactory=sharedFactory;
        this.count=rows.GetLongCount();
    }

    /// <summary>宣言メンバー名。</summary>
    public string Name { get; }
    public long LongCount {
        get {
            if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return global::System.Linq.Enumerable.LongCount(native.Rows);}
            var overlay=this.database.GetTransactionOverlay(this,create:false);
            if(overlay is not null){return overlay.LongCount;}
            return this.database.WithReadGate(()=>this.count);
        }
    }
    public int Count {
        get=>checked((int)this.LongCount);
    }
    /// <summary>生成query用の読取行ソース。所有Gateway取引または従来の読取保護を維持する。</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public DatabaseRows<TKey,TRecord> GetQueryRows(){
        ObjectDisposedException.ThrowIf(this.disposed,this);this.database.ValidateQueryAccess();
        if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return new(this.database,this,native.Rows);}return new(this.database,this,null);
    }
    /// <summary>Gateway共通scopeに属する読取り用native行ソース。</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public global::Seamless.Transactions.Collections.TransactionRows<TKey,TRecord> GetGatewayTransactionRows(){
        if(!this.database.TryGatewayVersionedCollection(this,false,out var native)){throw new InvalidOperationException("A versioned Gateway execution is required.");}return native.Rows;
    }
    public IEnumerable<TKey> Keys {
        get {
            if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return global::System.Linq.Enumerable.Select(native.Rows,this.keySelector);}
            var overlay=this.database.GetTransactionOverlay(this,create:false);
            if(overlay is not null){return overlay.Keys;}
            return this.EnumerateKeys();
        }
    }
    public TRecord this[TKey key]=>this.TryGetValue(key,out var record)?record:throw new KeyNotFoundException();

    public bool ContainsKey(TKey key)=>this.TryGetValue(key,out _);

    public bool TryGetValue(TKey key,[global::System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out TRecord record) {
        if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return native.TryGetValue(key,out record);}
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null){return overlay.TryGetValue(key,out record);}
        TRecord found=default;
        var result=this.database.WithReadGate(()=>this.rows.TryGetValue(key,out found));
        record=found;
        return result;
    }

    internal bool Add(TRecord record) {
        if(this.database.TryGatewayVersionedCollection(this,true,out var native)){return native.Add(record);}
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
        if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return native.Rows.GetEnumerator();}
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
        if(this.sharedFactory is not null){this.rows.Dispose();}
        this.ownedResources.Dispose();
    }
    internal void ReleaseSharedHandle()=>this.rows.Dispose();
    internal void AcquireSharedHandle(){this.rows=this.sharedFactory!();this.count=this.rows.GetLongCount();}
}
