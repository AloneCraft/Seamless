namespace Seamless.Database;

/// <summary>候補キーで探索できる物理テーブル。</summary>
public sealed class Table<TKey,TRecord>:global::System.Collections.Generic.IReadOnlyCollection<TRecord>,IDisposable
    where TKey:struct
    where TRecord:struct {
    private readonly DatabaseContainer database;
    private global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>? legacyRows;
    private global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> rows{get=>this.legacyRows??throw new InvalidOperationException("Use the generated versioned transaction collection.");set=>this.legacyRows=value;}
    private readonly Func<global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>>? sharedFactory;
    private readonly Func<TRecord,TKey> keySelector;
    private readonly IDisposable ownedResources;
    private bool disposed;
    internal Table(DatabaseContainer database,Func<TRecord,TKey> keySelector,IDisposable ownedResources,string name){this.database=database;this.keySelector=keySelector;this.ownedResources=ownedResources;this.Name=name;}

    internal Table(
        DatabaseContainer database,
        global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> rows,
        Func<TRecord,TKey> keySelector,
        IDisposable ownedResources,
        string name,Func<global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>>? sharedFactory=null) {
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
        this.sharedFactory=sharedFactory;
    }

    public string Name { get; }
    internal Action<TRecord?,TRecord?>? GatewayChanged{get;set;}
    internal Func<TRecord,TRecord>? GatewayNormalize{get;set;}
    internal IEqualityComparer<TKey>? GatewayKeyComparer{get;set;}
    internal bool KeysEqual(TKey first,TKey second)=>(this.GatewayKeyComparer??EqualityComparer<TKey>.Default).Equals(first,second);
    internal TKey RecordKey(TRecord record)=>this.keySelector(record);
    public long LongCount {
        get {
            if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return global::System.Linq.Enumerable.LongCount(native.Rows);}
            var overlay=this.database.GetTransactionOverlay(this,create:false);
            if(overlay is not null){return overlay.LongCount;}
            return this.database.WithReadGate(()=>this.rows.GetLongCount());
        }
    }
    public int Count {
        get=>checked((int)this.LongCount);
    }
    public IEnumerable<TKey> Keys=>this.EnumerateKeys();
    /// <summary>生成queryが方式を一度だけ選択する入口。新しい取引は開始しない。</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public DatabaseRows<TKey,TRecord> GetQueryRows(){
        ObjectDisposedException.ThrowIf(this.disposed,this);this.database.ValidateQueryAccess();
        if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return new(this.database,this,native.Rows);}return new(this.database,this,null);
    }
    /// <summary>Gateway共通scopeのnative行ソース。FLinqの融合・予算・query内workerを同じ取引へ渡す。</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public global::Seamless.Transactions.Collections.TransactionRows<TKey,TRecord> GetGatewayTransactionRows(){
        if(!this.database.TryGatewayVersionedCollection(this,false,out var native)){throw new InvalidOperationException("A versioned Gateway execution is required.");}return native.Rows;
    }
    public TRecord this[TKey key] {
        get=>this.TryGetValue(key,out var record)?record:throw new KeyNotFoundException();
        set {
            if(this.database.RequiresRelationTransaction){this.database.RelationWrite(()=>{this[key]=value;return true;});return;}
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.database.TryGatewayVersionedCollection(this,true,out var native)){
                if(this.GatewayNormalize is{} normalizeNative){value=normalizeNative(value);}TRecord? previous=null;if(this.GatewayChanged is not null&&native.TryGetValue(key,out var old)){previous=old;}native[key]=value;this.GatewayChanged?.Invoke(previous,value);return;
            }
            if(this.GatewayNormalize is{} normalize){value=normalize(value);}
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
        if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return native.TryGetValue(key,out record);}
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
        if(this.database.RequiresRelationTransaction){return this.database.RelationWrite(()=>this.Add(record));}
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.database.TryGatewayVersionedCollection(this,true,out var native)){
            if(this.GatewayNormalize is{} normalizeNative){record=normalizeNative(record);}var added=native.Add(record);if(added){this.GatewayChanged?.Invoke(null,record);}return added;
        }
        if(this.GatewayNormalize is{} normalize){record=normalize(record);}
        var key=this.keySelector(record);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        return overlay is not null
            ?overlay.Add(key,record)
            :this.database.WithWriteGate(()=>this.DirectAdd(key,record));
    }

    public bool Remove(TKey key) {
        if(this.database.RequiresRelationTransaction){return this.database.RelationWrite(()=>this.Remove(key));}
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.database.TryGatewayVersionedCollection(this,true,out var native)){
            if(this.GatewayChanged is null){return native.Remove(key);}if(!native.TryGetValue(key,out var old)){return false;}var removed=native.Remove(key);if(removed){this.GatewayChanged(old,null);}return removed;
        }
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        return overlay is not null
            ?overlay.Remove(key)
            :this.database.WithWriteGate(()=>this.DirectRemove(key));
    }

    /// <summary>キーの変更も一つの更新として通知する。重複先または存在しない元では変更しない。</summary>
    public bool Replace(TKey previousKey,TRecord record){
        if(this.database.RequiresRelationTransaction){return this.database.RelationWrite(()=>this.Replace(previousKey,record));}
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.database.TryGatewayVersionedCollection(this,true,out var native)){
            if(this.GatewayNormalize is{} normalizeNative){record=normalizeNative(record);}var next=this.keySelector(record);if(!native.TryGetValue(previousKey,out var old)){return false;}
            if(this.KeysEqual(previousKey,next)){native[next]=record;}else{if(native.ContainsKey(next)){return false;}_=native.Remove(previousKey);if(!native.Add(record)){throw new InvalidOperationException("The replacement key changed during its transaction operation.");}}
            this.GatewayChanged?.Invoke(old,record);return true;
        }
        if(this.GatewayNormalize is{} normalize){record=normalize(record);}var key=this.keySelector(record);var overlay=this.database.GetTransactionOverlay(this,create:true);if(overlay is not null){return overlay.Replace(previousKey,key,record);}return this.database.WithWriteGate(()=>{if(!this.DirectTryGetValue(previousKey,out _)){return false;}if(EqualityComparer<TKey>.Default.Equals(previousKey,key)){this.DirectSet(key,record);return true;}if(!this.DirectAdd(key,record)){return false;}_=this.DirectRemove(previousKey);return true;});
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
        if(this.database.TryGatewayVersionedCollection(this,false,out var native)){return native.Rows.GetEnumerator();}
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null){return overlay.Rows.GetEnumerator();}
        return this.database.Stream(()=>this.rows.Values.GetEnumerator());
    }
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();

    private IEnumerable<TKey> EnumerateKeys() {
        if(this.database.TryGatewayVersionedCollection(this,false,out var native)){foreach(var row in native.Rows){yield return this.keySelector(row);}yield break;}
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
        if(this.sharedFactory is not null){this.rows.Dispose();}
        this.ownedResources.Dispose();
    }
    internal void ReleaseSharedHandle()=>this.rows.Dispose();
    internal void AcquireSharedHandle(){this.rows=this.sharedFactory!();}
}
