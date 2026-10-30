using Seamless.Linq;
namespace Seamless.Database;

/// <summary>
/// 宣言コンテナのキーなしテーブル。Tsavorite HashSetを保持し、すべての読み取り、
/// 更新、列挙を所有DatabaseContainerのsessionとtransactionへ接続する。
/// </summary>
public sealed class Table<TRecord>:
    global::System.Collections.Generic.ICollection<TRecord>,
    global::Seamless.Collections.ISet<TRecord>,
    IDisposable
    where TRecord:notnull {
    private readonly DatabaseContainer database;
    private global::Seamless.Collections.Tsavorite.HashSet<TRecord> rows;
    private readonly Func<global::Seamless.Collections.Tsavorite.HashSet<TRecord>>? sharedFactory;
    private bool disposed;

    internal Table(
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
    public long LongCount {
        get {
            var overlay=this.database.GetTransactionOverlay(this,create:false);
            if(overlay is not null){return overlay.LongCount;}
            return this.database.WithReadGate(()=>this.rows.GetLongCount());
        }
    }
    public int Count=>checked((int)this.LongCount);
    public long GetLongCount()=>this.LongCount;

    public bool Add(TRecord item) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is not null){return overlay.Add(item);}
        return this.database.WithWriteGate(()=>this.rows.Add(item));
    }

    void global::System.Collections.Generic.ICollection<TRecord>.Add(TRecord item)=>this.Add(item);

    public bool Remove(TRecord item) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is not null){return overlay.Remove(item);}
        return this.database.WithWriteGate(()=>this.rows.Remove(item));
    }

    public bool Contains(TRecord item) {
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

    public void ExceptWith(IEnumerable<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(other,this)){this.Clear();return;}
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is null){this.database.WithWriteGate(()=>{this.rows.ExceptWith(other);return true;});return;}
        foreach(var item in other){_ = this.Remove(item);}
    }

    public void UnionWith(IEnumerable<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(other,this)){return;}
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is null){this.database.WithWriteGate(()=>{this.rows.UnionWith(other);return true;});return;}
        foreach(var item in other){_ = this.Add(item);}
    }

    public void IntersectWith(IEnumerable<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(other,this)){return;}
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is not null){this.ReplaceWithIntersection(overlay.Rows,other,overlay.Clear,overlay.Add);return;}
        this.database.WithWriteGate(()=>{
            this.ReplaceWithIntersection(this.rows,other,this.rows.Clear,this.rows.Add);
            return true;
        });
    }

    public void SymmetricExceptWith(IEnumerable<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(other,this)){this.Clear();return;}
        var overlay=this.database.GetTransactionOverlay(this,create:true);
        if(overlay is not null){this.ReplaceWithSymmetricDifference(overlay.Rows,other,overlay.Clear,overlay.Add);return;}
        this.database.WithWriteGate(()=>{
            this.ReplaceWithSymmetricDifference(this.rows,other,this.rows.Clear,this.rows.Add);
            return true;
        });
    }

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

    public IEnumerator<TRecord> GetEnumerator() {
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

    private void ReplaceWithIntersection(
        IEnumerable<TRecord> current,
        IEnumerable<TRecord> other,
        Action clear,
        Func<TRecord,bool> add) {
        using var result=this.database.ExecutionResources.Query(current).Intersect(other).ToStorage();
        clear();
        foreach(var item in result){_ = add(item);}
    }

    private void ReplaceWithSymmetricDifference(
        IEnumerable<TRecord> current,
        IEnumerable<TRecord> other,
        Action clear,
        Func<TRecord,bool> add) {
        var scope=this.database.ExecutionResources;
        using var distinctOther=scope.Query(other).Distinct().ToStorage();
        using var result=scope.Query(current).Except(distinctOther)
            .Concat(scope.Query(distinctOther).Except(current))
            .ToStorage();
        clear();
        foreach(var item in result){_ = add(item);}
    }

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
        this.rows.Dispose();
    }
}
