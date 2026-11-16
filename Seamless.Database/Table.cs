using Seamless.Linq;
namespace Seamless.Database;

/// <summary>
/// 宣言コンテナのキーなしテーブル。Tsavorite HashSetを保持し、すべての読み取り、
/// 更新、列挙を所有DatabaseContainerのsessionとtransactionへ接続する。
/// </summary>
public sealed class StoredTable<TRecord>:Table<TRecord>,
    global::System.Collections.Generic.ICollection<TRecord>,
    global::Seamless.Collections.ISet<TRecord>,
    IDisposable
    where TRecord:notnull {
    private readonly DatabaseContainer database;
    private global::Seamless.Collections.Tsavorite.HashSet<TRecord>? legacyRows;
    private global::Seamless.Collections.Tsavorite.HashSet<TRecord> rows{get=>this.legacyRows??throw new InvalidOperationException("Use the generated versioned transaction collection.");set=>this.legacyRows=value;}
    private readonly IDisposable? versionedOwner;
    private readonly Func<global::Seamless.Collections.Tsavorite.HashSet<TRecord>>? sharedFactory;
    private bool disposed;
    internal StoredTable(DatabaseContainer database,IDisposable versionedOwner,string name){this.database=database;this.versionedOwner=versionedOwner;this.Name=name;}

    internal StoredTable(
        DatabaseContainer database,
        global::Seamless.Collections.Tsavorite.HashSet<TRecord> rows,
        string name,Func<global::Seamless.Collections.Tsavorite.HashSet<TRecord>>? sharedFactory=null) {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this.database=database;
        this.rows=rows;
        this.Name=name;
        this.sharedFactory=sharedFactory;
    }

    /// <summary>宣言プロパティ名。</summary>
    public string Name { get; }
    public bool IsReadOnly=>false;
    public override long LongCount {
        get {
            if(this.database.TryGetImplicitCollection(this,out var implicitRows)){return global::System.Linq.Enumerable.LongCount(implicitRows);}
            var overlay=this.database.GetTransactionOverlay(this,create:false);
            if(overlay is not null){return overlay.LongCount;}
            return this.database.WithReadGate(()=>this.rows.GetLongCount());
        }
    }

    public long GetLongCount()=>this.LongCount;

    public override bool Add(TRecord item) {
        if(this.database.TryWriteImplicitly(this,collection=>collection.Add(item),out var implicitAdded)){return implicitAdded;}
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is not null){return overlay.Add(item);}
        return this.database.WithWriteGate(()=>this.rows.Add(item));
    }

    void global::System.Collections.Generic.ICollection<TRecord>.Add(TRecord item)=>this.Add(item);

    public override bool Remove(TRecord item) {
        if(this.database.TryWriteImplicitly(this,collection=>collection.Remove(item),out var implicitRemoved)){return implicitRemoved;}
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is not null){return overlay.Remove(item);}
        return this.database.WithWriteGate(()=>this.rows.Remove(item));
    }

    public override bool Contains(TRecord item) {
        if(this.database.TryGetImplicitCollection(this,out var implicitRows)){return implicitRows.ContainsKey(item);}
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null){return overlay.Contains(item);}
        return this.database.WithReadGate(()=>this.rows.Contains(item));
    }

    public void Clear() {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is not null){overlay.Clear();return;}
        this.database.WithWriteGate(()=>{this.rows.Clear();return true;});
    }

    public void CopyTo(TRecord[] array,int arrayIndex) {
        this.database.WithReadGate(()=>{this.rows.CopyTo(array,arrayIndex);return true;});
    }

    // [関係代入] 集合を書き換える演算は、関係代入と同じ差分の適用で書く。全部消して入れ直すことはしない。
    public void ExceptWith(IEnumerable<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(other,this)){this.Clear();return;}
        this.Assign(this.Except(other));
    }

    public void UnionWith(IEnumerable<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(other,this)){return;}
        this.Assign(this.Union(other));
    }

    public void IntersectWith(IEnumerable<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(other,this)){return;}
        this.Assign(this.Intersect(other));
    }

    public void SymmetricExceptWith(IEnumerable<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(other,this)){this.Clear();return;}
        // 両方にある行は消し、other にだけある行は足す。どちらの行かは書き込み前に全部決める。
        var both=new List<TRecord>();var onlyOther=new List<TRecord>();
        foreach(var item in new global::System.Collections.Generic.HashSet<TRecord>(other)){if(this.Contains(item)){both.Add(item);}else{onlyOther.Add(item);}}
        this.Assign(this.Except(both).Union(onlyOther));
    }

    /// <summary>関係代入。取引の外なら、この1回の代入を1つの暗黙の取引で確定する。取引が開いている流れでは例外。</summary>
    public override void Assign(Table<TRecord,TRecord> value){
        ArgumentNullException.ThrowIfNull(value);ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.database.TryWriteImplicitly(this,collection=>{ApplyAssignment(collection,this,value);return true;},out _)){return;}
        ApplyAssignment(this,this,value);
    }
    // [関係代入/FLinq] 取引の外なら、1回の呼び出しを1つの暗黙の取引で確定する。
    public override void AssignUnion(params IEnumerable<TRecord>[] rows){if(this.database.TryWriteImplicitly(this,collection=>{collection.AssignUnion(rows);return true;},out _)){return;}base.AssignUnion(rows);}
    public override void AssignExcept(params IEnumerable<TRecord>[] rows){if(this.database.TryWriteImplicitly(this,collection=>{collection.AssignExcept(rows);return true;},out _)){return;}base.AssignExcept(rows);}
    public override void AssignWhere(Func<TRecord,bool> keep){if(this.database.TryWriteImplicitly(this,collection=>{collection.AssignWhere(keep);return true;},out _)){return;}base.AssignWhere(keep);}
    public override void AssignRows(IEnumerable<TRecord> rows){if(this.database.TryWriteImplicitly(this,collection=>{collection.AssignRows(rows);return true;},out _)){return;}base.AssignRows(rows);}
    public bool IsSubsetOf(IEnumerable<TRecord> other)=>this.database.WithReadGate(()=>this.IsSubsetOfCore(other,proper:false));
    public bool IsSupersetOf(IEnumerable<TRecord> other)=>this.database.WithReadGate(()=>this.IsSupersetOfCore(other,proper:false));
    public bool IsProperSubsetOf(IEnumerable<TRecord> other)=>this.database.WithReadGate(()=>this.IsSubsetOfCore(other,proper:true));
    public bool IsProperSupersetOf(IEnumerable<TRecord> other)=>this.database.WithReadGate(()=>this.IsSupersetOfCore(other,proper:true));
    public bool Overlaps(IEnumerable<TRecord> other)=>this.database.WithReadGate(()=>{
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){if(this.Contains(item)){return true;}}
        return false;
    });
    public bool SetEquals(IEnumerable<TRecord> other)=>this.database.WithReadGate(()=>{
        ArgumentNullException.ThrowIfNull(other);
        var set=other as global::System.Collections.Generic.ISet<TRecord>??new global::System.Collections.Generic.HashSet<TRecord>(other);
        return this.LongCount==set.Count&&this.IsSubsetOfCore(set,proper:false);
    });

    public override IEnumerator<TRecord> GetEnumerator() {
        if(this.database.TryGetImplicitCollection(this,out var implicitRows)){return implicitRows.GetEnumerator();}
        var overlay=this.database.GetTransactionOverlay(this,create:false);
        if(overlay is not null){return overlay.Rows.GetEnumerator();}
        return this.database.Stream(()=>((IEnumerable<TRecord>)this.rows).GetEnumerator());
    }

    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();

    internal long DirectLongCount=>this.rows.GetLongCount();
    internal IEnumerable<TRecord> DirectRows=>this.rows;
    internal bool DirectContains(TRecord item)=>this.rows.Contains(item);
    internal bool DirectAdd(TRecord item)=>this.rows.Add(item);
    internal bool DirectRemove(TRecord item)=>this.rows.Remove(item);
    internal void DirectClear()=>this.rows.Clear();
    internal void ReleaseSharedHandle()=>this.rows.Dispose();
    internal void AcquireSharedHandle(){this.rows=this.sharedFactory!();}

    private bool IsSubsetOfCore(IEnumerable<TRecord> other,bool proper) {
        ArgumentNullException.ThrowIfNull(other);
        var set=other as global::System.Collections.Generic.ISet<TRecord>??new global::System.Collections.Generic.HashSet<TRecord>(other);
        if(proper&&this.LongCount>=set.Count){return false;}
        if(!proper&&this.LongCount>set.Count){return false;}
        foreach(var item in this){if(!set.Contains(item)){return false;}}
        return true;
    }

    private bool IsSupersetOfCore(IEnumerable<TRecord> other,bool proper) {
        ArgumentNullException.ThrowIfNull(other);
        var set=other as global::System.Collections.Generic.ISet<TRecord>??new global::System.Collections.Generic.HashSet<TRecord>(other);
        if(proper&&this.LongCount<=set.Count){return false;}
        if(!proper&&this.LongCount<set.Count){return false;}
        foreach(var item in set){if(!this.Contains(item)){return false;}}
        return true;
    }

    public void Dispose() {
        if(this.disposed){return;}
        this.disposed=true;
        if(this.versionedOwner is not null){this.versionedOwner.Dispose();}else{this.rows.Dispose();}
    }
}
