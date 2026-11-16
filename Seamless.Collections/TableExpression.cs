namespace Seamless.Database;

/// <summary>表の式の木。列挙は集合として行う(同じ行は1回だけ返す)。順序は意味を持たない。</summary>
internal abstract class TableExpression<TKey,TRecord> where TKey:notnull{
    internal abstract IEnumerable<TRecord> Evaluate();

    /// <summary>保存された表・取引の中の表そのもの。</summary>
    internal sealed class Source(Table<TKey,TRecord> table):TableExpression<TKey,TRecord>{
        internal Table<TKey,TRecord> Table{get;}=table;
        internal override IEnumerable<TRecord> Evaluate()=>this.Table;
    }

    /// <summary>行の集まり(new(rows)・コレクション式・暗黙の変換)。</summary>
    internal sealed class Rows(IEnumerable<TRecord> rows):TableExpression<TKey,TRecord>{
        internal IEnumerable<TRecord> Values{get;}=rows;
        internal override IEnumerable<TRecord> Evaluate(){
            var seen=new global::System.Collections.Generic.HashSet<TRecord>();
            foreach(var row in this.Values){if(seen.Add(row)){yield return row;}}
        }
    }

    internal sealed class Filter(TableExpression<TKey,TRecord> source,Func<TRecord,bool> predicate):TableExpression<TKey,TRecord>{
        internal TableExpression<TKey,TRecord> Source{get;}=source;
        internal Func<TRecord,bool> Predicate{get;}=predicate;
        internal override IEnumerable<TRecord> Evaluate(){foreach(var row in this.Source.Evaluate()){if(this.Predicate(row)){yield return row;}}}
    }

    /// <summary>左の式と、右の行の集まりを組み合わせる演算(Union・Except・Intersect)。</summary>
    internal abstract class Binary(TableExpression<TKey,TRecord> left,IEnumerable<TRecord> right):TableExpression<TKey,TRecord>{
        internal TableExpression<TKey,TRecord> Left{get;}=left;
        internal IEnumerable<TRecord> Right{get;}=right;
    }

    internal sealed class Union(TableExpression<TKey,TRecord> left,IEnumerable<TRecord> right):Binary(left,right){
        internal override IEnumerable<TRecord> Evaluate(){
            var seen=new global::System.Collections.Generic.HashSet<TRecord>();
            foreach(var row in this.Left.Evaluate()){if(seen.Add(row)){yield return row;}}
            foreach(var row in this.Right){if(seen.Add(row)){yield return row;}}
        }
    }

    internal sealed class Except(TableExpression<TKey,TRecord> left,IEnumerable<TRecord> right):Binary(left,right){
        internal override IEnumerable<TRecord> Evaluate(){
            var removed=new global::System.Collections.Generic.HashSet<TRecord>(this.Right);
            foreach(var row in this.Left.Evaluate()){if(!removed.Contains(row)){yield return row;}}
        }
    }

    internal sealed class Intersect(TableExpression<TKey,TRecord> left,IEnumerable<TRecord> right):Binary(left,right){
        internal override IEnumerable<TRecord> Evaluate(){
            var kept=new global::System.Collections.Generic.HashSet<TRecord>(this.Right);
            foreach(var row in this.Left.Evaluate()){if(kept.Contains(row)){yield return row;}}
        }
    }
}

/// <summary>
/// 関係代入の適用。表の中身を新しい値にするための差分を求め、全部検査してから書く。
/// 実行時に式の木を読む経路(Apply)と、FLinq が書き換えた呼び出し(Table.AssignUnion など)が同じ処理を使う。
/// </summary>
internal static class TableAssignment{
    /// <param name="target">書き込み先(取引の中の表、または保存された表)。</param>
    /// <param name="identity">式の根が「左辺と同じ表」かを見分ける表。暗黙の取引では保存された表、それ以外は target と同じ。</param>
    /// <param name="value">新しい中身。</param>
    internal static void Apply<TKey,TRecord>(Table<TKey,TRecord> target,Table<TKey,TRecord> identity,Table<TKey,TRecord> value) where TKey:notnull{
        if(value.Expression is null&&(target.RefersToSameTable(value)||identity.RefersToSameTable(value))){return;}
        var chain=new List<TableExpression<TKey,TRecord>>();
        var root=value.AsExpression();
        // 左に連なる演算を外側から集め、根(左端)まで降りる。
        while(true){
            if(root is TableExpression<TKey,TRecord>.Filter filter){chain.Add(filter);root=filter.Source;continue;}
            if(root is TableExpression<TKey,TRecord>.Binary binary){chain.Add(binary);root=binary.Left;continue;}
            break;
        }
        // 書いた順(内側から)に並べ直す。結果は集合なので、順序は結果に影響しない。
        chain.Reverse();
        if(root is TableExpression<TKey,TRecord>.Source source&&(target.RefersToSameTable(source.Table)||identity.RefersToSameTable(source.Table))&&chain.Count>0){
            if(chain.TrueForAll(static node=>node is TableExpression<TKey,TRecord>.Union)){
                AddMissing(target,chain.Select(static node=>((TableExpression<TKey,TRecord>.Binary)node).Right));return;
            }
            if(chain.TrueForAll(static node=>node is TableExpression<TKey,TRecord>.Except)){
                RemoveListed(target,chain.Select(static node=>((TableExpression<TKey,TRecord>.Binary)node).Right));return;
            }
            if(chain.TrueForAll(static node=>node is TableExpression<TKey,TRecord>.Filter or TableExpression<TKey,TRecord>.Except or TableExpression<TKey,TRecord>.Intersect)){
                // 右の行の集まりは、表を読む前に一度だけ集める。
                var tests=new List<Func<TRecord,bool>>(chain.Count);
                foreach(var node in chain){
                    switch(node){
                        case TableExpression<TKey,TRecord>.Filter filter:tests.Add(filter.Predicate);break;
                        case TableExpression<TKey,TRecord>.Except except:{var set=new global::System.Collections.Generic.HashSet<TRecord>(except.Right);tests.Add(row=>!set.Contains(row));break;}
                        case TableExpression<TKey,TRecord>.Intersect intersect:{var set=new global::System.Collections.Generic.HashSet<TRecord>(intersect.Right);tests.Add(row=>set.Contains(row));break;}
                    }
                }
                RemoveRejected(target,tests);return;
            }
        }
        Replace(target,value.AsExpression().Evaluate());
    }

    // T = T.Union(R1).Union(R2)…: 右の行のキーを表で引き、無い行だけを足す。表の全件は読まない。
    internal static void AddMissing<TKey,TRecord>(Table<TKey,TRecord> target,IEnumerable<IEnumerable<TRecord>> sources) where TKey:notnull{
        var comparer=EqualityComparer<TRecord>.Default;
        var staged=new Dictionary<TKey,TRecord>();
        foreach(var source in sources){
            foreach(var row in source){
                var key=target.KeyOf(row);
                if(staged.TryGetValue(key,out var pending)){
                    if(!comparer.Equals(pending,row)){throw Violation(key);}
                    continue;
                }
                if(target.TryGetValue(key,out var existing)){
                    if(!comparer.Equals(existing,row)){throw Violation(key);}
                    continue;
                }
                staged.Add(key,row);
            }
        }
        foreach(var row in staged.Values){
            if(!target.Add(row)){throw Changed();}
        }
    }

    // T = T.Except(R1).Except(R2)…: 右の行のうち、表にあって全列が等しい行だけを消す。表の全件は読まない。
    internal static void RemoveListed<TKey,TRecord>(Table<TKey,TRecord> target,IEnumerable<IEnumerable<TRecord>> sources) where TKey:notnull{
        var comparer=EqualityComparer<TRecord>.Default;
        var removals=new global::System.Collections.Generic.HashSet<TKey>();
        foreach(var source in sources){
            foreach(var row in source){
                var key=target.KeyOf(row);
                if(target.TryGetValue(key,out var existing)&&comparer.Equals(existing,row)){removals.Add(key);}
            }
        }
        foreach(var key in removals){_=target.Remove(key);}
    }

    // T = T.Where(p)・Except・Intersect の組み合わせ: 表を1回読み、どれかの条件から外れる行を消す。足す行は無い。
    internal static void RemoveRejected<TKey,TRecord>(Table<TKey,TRecord> target,IReadOnlyList<Func<TRecord,bool>> tests) where TKey:notnull{
        var removals=new List<TKey>();
        foreach(var row in target){
            foreach(var test in tests){
                if(!test(row)){removals.Add(target.KeyOf(row));break;}
            }
        }
        foreach(var key in removals){_=target.Remove(key);}
    }

    // 一般の置き換え: 新しい中身をキーで集め、古い中身と突き合わせて、消す・上書きする・足す行だけを書く。
    internal static void Replace<TKey,TRecord>(Table<TKey,TRecord> target,IEnumerable<TRecord> rows) where TKey:notnull{
        var comparer=EqualityComparer<TRecord>.Default;
        var next=new Dictionary<TKey,TRecord>();
        foreach(var row in rows){
            var key=target.KeyOf(row);
            if(next.TryGetValue(key,out var other)){
                if(!comparer.Equals(other,row)){throw Violation(key);}
                continue;
            }
            next.Add(key,row);
        }
        var removals=new List<TKey>();
        var updates=new List<KeyValuePair<TKey,TRecord>>();
        foreach(var row in target){
            var key=target.KeyOf(row);
            if(!next.Remove(key,out var replacement)){removals.Add(key);continue;}
            if(!comparer.Equals(row,replacement)){updates.Add(new(key,replacement));}
        }
        foreach(var key in removals){_=target.Remove(key);}
        foreach(var pair in updates){target[pair.Key]=pair.Value;}
        foreach(var row in next.Values){
            if(!target.Add(row)){throw Changed();}
        }
    }

    private static InvalidOperationException Changed()=>new("The table changed while the relational assignment was being applied.");

    private static PrimaryKeyViolationException Violation<TKey>(TKey key) where TKey:notnull=>
        new(key,$"The relational assignment would leave two different rows with the same primary key '{key}'. Nothing was written.");
}