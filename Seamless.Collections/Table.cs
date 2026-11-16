using System.ComponentModel;
namespace Seamless.Database;

/// <summary>
/// 表(関係)。保存された表・取引の中の表・表の式(Where/Union/Except/Intersect の結果や行の集まり)を同じ型で表す。
/// </summary>
/// <remarks>
/// 関係代入(Document/refactoring-designs/20261117-関係代入.md)の型。
/// 表のプロパティは get と set が同じ型なので、<c>T = T.Union(rows)</c> を書けるよう、式もこの型で返す。
/// 行の等しさは行の型の Equals(全列の値の比較)に従う。主キーの一意性は代入の適用時に別に検査する。
/// 表ではない式へ書き込むと例外になる(解析器はコンパイルエラーにする)。
/// </remarks>
[CollectionBuilder(typeof(TableBuilder),nameof(TableBuilder.Create))]
public class Table<TKey,TRecord>:IEnumerable<TRecord> where TKey:notnull{
    private readonly TableExpression<TKey,TRecord>? expression;

    /// <summary>保存された表・取引の中の表(派生の型)が使う。</summary>
    protected Table(){}

    /// <summary>行の集まりを表す式。<c>T = new(query)</c> の置き換えに使う。重複した行は1行として扱う。</summary>
    public Table(IEnumerable<TRecord> rows){
        ArgumentNullException.ThrowIfNull(rows);
        this.expression=new TableExpression<TKey,TRecord>.Rows(rows);
    }

    private protected Table(TableExpression<TKey,TRecord> expression){this.expression=expression;}

    internal TableExpression<TKey,TRecord>? Expression=>this.expression;
    internal TableExpression<TKey,TRecord> AsExpression()=>this.expression??new TableExpression<TKey,TRecord>.Source(this);

    /// <summary>表ではなく式か。式には書き込めない。</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsExpression=>this.expression is not null;

    public static implicit operator Table<TKey,TRecord>(List<TRecord> rows)=>new(rows);
    public static implicit operator Table<TKey,TRecord>(TRecord[] rows)=>new(rows);
    public static implicit operator Table<TKey,TRecord>(global::System.Collections.Generic.HashSet<TRecord> rows)=>new(rows);

    /// <summary>条件を満たす行だけの表。表へ代入すると、満たさない行を消す。</summary>
    public Table<TKey,TRecord> Where(Func<TRecord,bool> predicate){
        ArgumentNullException.ThrowIfNull(predicate);
        return new(new TableExpression<TKey,TRecord>.Filter(this.AsExpression(),predicate));
    }
    /// <summary>和集合。表へ代入すると、無い行だけを足す。同じキーで中身が違う行が残る結果は主キーの違反。</summary>
    public Table<TKey,TRecord> Union(IEnumerable<TRecord> rows){
        ArgumentNullException.ThrowIfNull(rows);
        return new(new TableExpression<TKey,TRecord>.Union(this.AsExpression(),rows));
    }
    /// <summary>差集合。表へ代入すると、rows にある行(全列が等しい行)だけを消す。</summary>
    public Table<TKey,TRecord> Except(IEnumerable<TRecord> rows){
        ArgumentNullException.ThrowIfNull(rows);
        return new(new TableExpression<TKey,TRecord>.Except(this.AsExpression(),rows));
    }
    /// <summary>積集合。表へ代入すると、rows に無い行を消す。</summary>
    public Table<TKey,TRecord> Intersect(IEnumerable<TRecord> rows){
        ArgumentNullException.ThrowIfNull(rows);
        return new(new TableExpression<TKey,TRecord>.Intersect(this.AsExpression(),rows));
    }

    public virtual TRecord this[TKey key]{
        get=>this.TryGetValue(key,out var row)?row:throw new KeyNotFoundException();
        set=>throw this.WriteError();
    }
    public virtual bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TRecord row){
        if(this.expression is not null){throw new NotSupportedException("A table expression cannot be searched by key. Assign it to a table, or enumerate it.");}
        throw new NotSupportedException("This table does not support key lookup.");
    }
    public virtual bool ContainsKey(TKey key)=>this.TryGetValue(key,out _);
    /// <summary>行全体が等しい行を含むか。キーだけの一致では含まない。</summary>
    public virtual bool Contains(TRecord row){
        if(this.expression is not null){
            var comparer=EqualityComparer<TRecord>.Default;
            foreach(var current in this.expression.Evaluate()){if(comparer.Equals(current,row)){return true;}}
            return false;
        }
        return this.TryGetValue(this.KeyOf(row),out var existing)&&EqualityComparer<TRecord>.Default.Equals(existing,row);
    }
    /// <summary>行数。保存された表は保存先の件数、取引の中の表と式は全件を数える(O(n))。2026-11-17 利用者決定で基底に置く。</summary>
    public virtual long LongCount{get{long count=0;foreach(var _ in this){count++;}return count;}}
    public int Count=>checked((int)this.LongCount);
    /// <summary>行を足す。同じキーの行が既にあれば何もせず false を返す。</summary>
    public virtual bool Add(TRecord row)=>throw this.WriteError();
    public virtual bool Remove(TKey key)=>throw this.WriteError();

    // [共通の操作] 保存された表だけが持っていた操作を、2026-11-17 利用者決定で基底に置く。
    // 基底の実装は TryGetValue・添字・Add・Remove の組み合わせ。保存された表は今の実装で上書きする。

    /// <summary>全行のキー。</summary>
    public virtual IEnumerable<TKey> Keys{get{foreach(var row in this){yield return this.KeyOf(row);}}}

    /// <summary>行のキー。</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TKey GetKey(TRecord row)=>this.KeyOf(row);

    /// <summary>行全体が等しいか(Remove(行) の判定)。保存された表は保存形式での比較で上書きする。</summary>
    protected internal virtual bool RowsEqual(TRecord first,TRecord second)=>EqualityComparer<TRecord>.Default.Equals(first,second);

    /// <summary>2つのキーが同じ行を指すか。</summary>
    protected internal virtual bool AreSameKey(TKey first,TKey second)=>EqualityComparer<TKey>.Default.Equals(first,second);

    /// <summary>キーの変更も一つの更新として扱う。重複先または存在しない元では変更しない。</summary>
    public virtual bool Replace(TKey previousKey,TRecord record){
        if(!this.TryGetValue(previousKey,out _)){return false;}
        var next=this.KeyOf(record);
        if(this.AreSameKey(previousKey,next)){this[next]=record;return true;}
        if(this.ContainsKey(next)){return false;}
        _=this.Remove(previousKey);
        if(!this.Add(record)){throw new InvalidOperationException("The replacement key changed during the operation.");}
        return true;
    }

    public virtual void Update(TKey key,Func<TRecord,TRecord> updater){
        ArgumentNullException.ThrowIfNull(updater);
        if(!this.TryGetValue(key,out var current)){throw new KeyNotFoundException($"Key '{key}' was not found.");}
        var updated=updater(current);var newKey=this.KeyOf(updated);
        if(this.AreSameKey(key,newKey)){this[key]=updated;return;}
        if(!this.Replace(key,updated)){throw new InvalidOperationException($"Cannot replace key '{key}' with '{newKey}': destination key already exists or conflict occurred.");}
    }

    public virtual MergeResult Merge(TKey key,Func<TKey,TRecord> create,Func<TRecord,TRecord> update){
        ArgumentNullException.ThrowIfNull(create);ArgumentNullException.ThrowIfNull(update);
        if(this.TryGetValue(key,out var current)){
            var updated=update(current);var newKey=this.KeyOf(updated);
            if(this.AreSameKey(key,newKey)){this[key]=updated;}
            else if(!this.Replace(key,updated)){throw new InvalidOperationException($"Cannot replace key '{key}' with '{newKey}' during merge: destination key already exists.");}
            return MergeResult.Updated;
        }
        if(!this.Add(create(key))){throw new InvalidOperationException($"Cannot add created record with key '{key}' during merge.");}
        return MergeResult.Added;
    }

    /// <summary>行のキー。保存された表・取引の中の表が持つ。</summary>
    protected internal virtual TKey KeyOf(TRecord row){
        if(this.expression is not null){throw new NotSupportedException("A table expression has no key selector.");}
        throw new NotSupportedException("This table does not define a key selector.");
    }

    /// <summary>other がこの表と同じ表(同じ保存先、取引の中なら同じ取引)を指すか。関係代入で「左辺と同じ表を根に持つ式」を見分ける。</summary>
    protected internal virtual bool RefersToSameTable(Table<TKey,TRecord> other)=>ReferenceEquals(this,other);

    /// <summary>関係代入の入口。表のプロパティの set から呼ぶ。中身を value にするための差分(追加・削除・上書き)だけを書く。</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void Assign(Table<TKey,TRecord> value){
        ArgumentNullException.ThrowIfNull(value);
        if(this.expression is not null){throw this.WriteError();}
        TableAssignment.Apply(this,this,value);
    }

    // [関係代入/FLinq] FLinq が関係代入の文を書き換えた後に呼ぶ入口。式の木を作らずに、形の分かった差分の処理を直接行う。
    // T = T.Union(R1)…Union(Rn) を書き換えた形。右の行のうち表に無い行だけを足す。
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void AssignUnion(params IEnumerable<TRecord>[] rows){ArgumentNullException.ThrowIfNull(rows);if(this.expression is not null){throw this.WriteError();}TableAssignment.AddMissing(this,rows);}
    // T = T.Except(R1)…Except(Rn) を書き換えた形。右の行のうち表にあって全列が等しい行だけを消す。
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void AssignExcept(params IEnumerable<TRecord>[] rows){ArgumentNullException.ThrowIfNull(rows);if(this.expression is not null){throw this.WriteError();}TableAssignment.RemoveListed(this,rows);}
    // T = T.Where(p) を書き換えた形。条件を満たさない行を消す。
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void AssignWhere(Func<TRecord,bool> keep){ArgumentNullException.ThrowIfNull(keep);if(this.expression is not null){throw this.WriteError();}TableAssignment.RemoveRejected(this,[keep]);}
    // T = new(rows)・[..rows] を書き換えた形。中身を rows にする差分だけを書く。
    [EditorBrowsable(EditorBrowsableState.Never)]
    public virtual void AssignRows(IEnumerable<TRecord> rows){ArgumentNullException.ThrowIfNull(rows);if(this.expression is not null){throw this.WriteError();}TableAssignment.Replace(this,rows);}

    /// <summary>派生の表が、書き込み先(target)を取引の中の表に替えて関係代入を適用するときに使う。identity は「左辺と同じ表」を見分ける表。</summary>
    protected static void ApplyAssignment(Table<TKey,TRecord> target,Table<TKey,TRecord> identity,Table<TKey,TRecord> value){
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(identity);ArgumentNullException.ThrowIfNull(value);
        if(target.expression is not null){throw target.WriteError();}
        TableAssignment.Apply(target,identity,value);
    }

    public virtual IEnumerator<TRecord> GetEnumerator(){
        if(this.expression is not null){return this.expression.Evaluate().GetEnumerator();}
        throw new NotSupportedException("This table does not support enumeration.");
    }
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();

    private protected Exception WriteError()=>this.expression is not null
        ?new InvalidOperationException("This value is a table expression, not a table, so it cannot be written. Assign the expression to a table property instead.")
        :new NotSupportedException("This table does not support writing.");
}

/// <summary>キーの無い表。行そのものをキーとして扱う。</summary>
[CollectionBuilder(typeof(TableBuilder),nameof(TableBuilder.Create))]
public class Table<TRecord>:Table<TRecord,TRecord> where TRecord:notnull{
    protected Table(){}
    public Table(IEnumerable<TRecord> rows):base(rows){}
    private protected Table(TableExpression<TRecord,TRecord> expression):base(expression){}

    public static implicit operator Table<TRecord>(List<TRecord> rows)=>new(rows);
    public static implicit operator Table<TRecord>(TRecord[] rows)=>new(rows);
    public static implicit operator Table<TRecord>(global::System.Collections.Generic.HashSet<TRecord> rows)=>new(rows);

    public new Table<TRecord> Where(Func<TRecord,bool> predicate){
        ArgumentNullException.ThrowIfNull(predicate);
        return new(new TableExpression<TRecord,TRecord>.Filter(this.AsExpression(),predicate));
    }
    public new Table<TRecord> Union(IEnumerable<TRecord> rows){
        ArgumentNullException.ThrowIfNull(rows);
        return new(new TableExpression<TRecord,TRecord>.Union(this.AsExpression(),rows));
    }
    public new Table<TRecord> Except(IEnumerable<TRecord> rows){
        ArgumentNullException.ThrowIfNull(rows);
        return new(new TableExpression<TRecord,TRecord>.Except(this.AsExpression(),rows));
    }
    public new Table<TRecord> Intersect(IEnumerable<TRecord> rows){
        ArgumentNullException.ThrowIfNull(rows);
        return new(new TableExpression<TRecord,TRecord>.Intersect(this.AsExpression(),rows));
    }

    public override TRecord this[TRecord key]{
        get=>this.Contains(key)?key:throw new KeyNotFoundException();
        set{
            if(!EqualityComparer<TRecord>.Default.Equals(key,value)){throw new ArgumentException("A keyless table uses the row itself as its key.",nameof(value));}
            _=this.Add(value);
        }
    }
    public override bool TryGetValue(TRecord key,[MaybeNullWhen(false)] out TRecord row){
        if(this.Contains(key)){row=key;return true;}
        row=default;return false;
    }
    public override bool ContainsKey(TRecord key)=>this.Contains(key);
    protected internal override TRecord KeyOf(TRecord row)=>row;
}

/// <summary>コレクション式(<c>T = [a, b]</c>・<c>T = [..rows]</c>)から表の式を作る。</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TableBuilder{
    public static Table<TKey,TRecord> Create<TKey,TRecord>(ReadOnlySpan<TRecord> rows) where TKey:notnull=>new(rows.ToArray());
    public static Table<TRecord> Create<TRecord>(ReadOnlySpan<TRecord> rows) where TRecord:notnull=>new(rows.ToArray());
}

/// <summary>関係代入の結果で、同じ主キーに中身の違う行が2つ以上残るとき。代入による変更は1行も書かない。</summary>
public sealed class PrimaryKeyViolationException(object key,string message):InvalidOperationException(message){
    public object Key{get;}=key;
}

/// <summary>表の操作のうち、キーの無い表で呼び分けがぶつかるもの。</summary>
public static class TableExtensions{
    /// <summary>行全体が等しい行があれば消す。キーだけ一致して中身が違う行は消さない。</summary>
    /// <remarks>キーの無い表では Remove(キー) と同じ形になるので、拡張メソッドにして呼び分けがぶつからないようにする。</remarks>
    public static bool Remove<TKey,TRecord>(this Table<TKey,TRecord> table,TRecord row) where TKey:notnull{
        ArgumentNullException.ThrowIfNull(table);
        var key=table.GetKey(row);
        return table.TryGetValue(key,out var existing)&&table.RowsEqual(existing,row)&&table.Remove(key);
    }
}