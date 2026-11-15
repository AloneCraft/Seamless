using SeamlessSet = Seamless.Collections;

namespace Seamless.Linq;

/// <summary>
/// 読取専用の集合<see cref="SeamlessSet.ISet{T}"/>を受け取り、結果も集合として返す演算。
/// 結果はコピーを作らず、中身を読んだ時点で元の集合から計算する式として表す。
/// <c>Select</c>・<c>SelectMany</c>・結合・結果を作る関数を渡す<c>GroupBy</c>・<c>Union</c>の結果は重複を除いて列挙する。
/// </summary>
/// <remarks>
/// 重複除去と<c>Join</c>の内側の索引はメモリ上の<see cref="System.Collections.Generic.HashSet{T}"/>で行い、
/// 等しさは<see cref="EqualityComparer{T}.Default"/>で判定する。大きな結果をディスクへ逃がす処理と、
/// DBの符号化に基づく等しさとはまだ接続していない（暫定）。FLinqが呼出元で展開する場合はこの実装を通らない。
/// 演算は、集合を受けて集合を返す意味が決まるもの(関係代数)だけを置く。並び順に依存する演算(First・Take・OrderBy など)と、
/// どの要素を残すかが並び順で変わる演算(DistinctBy・UnionBy)、畳み込みの順で結果が変わる演算(AggregateBy)は置かない。
/// 並び順のある列として扱うときは AsEnumerable() で明示的に外へ出る(解析器 SEAMLINQ001 の規則)。
/// 集合から1つの値を求める演算(Count・Contains・Any・Sum など)も置かない。ISet は Count プロパティと Contains を持ち、
/// それ以外は System.Linq と同じ答えになる。Set に置くと、FLinq が展開の対象を選ぶ判定(本体に System.Linq の呼び出しがあるか)から外れる。
/// Append・Prepend・Concat(解析器が並び順の演算として扱う)と、型引数を2つ取る Cast・OfType も置かない。
/// 演算の名前と引数の形は System.Linq.Enumerable と同じにする。FLinq は Set の呼び出しを同じ名前の System.Linq の演算へ書き換えて展開する(SetCallLowering)。
/// </remarks>
public static partial class Set {
    /// <summary>条件を満たす要素だけの部分集合。重複は元から存在しないため除去しない。</summary>
    public static SeamlessSet.ISet<TSource> Where<TSource>(this SeamlessSet.ISet<TSource> source,Func<TSource,bool> predicate) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(predicate);
        return new WhereSet<TSource>(source,predicate);
    }

    /// <summary>各要素を写した集合。写した結果の重複は除く。</summary>
    public static SeamlessSet.ISet<TResult> Select<TSource, TResult>(this SeamlessSet.ISet<TSource> source,Func<TSource,TResult> selector) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(selector);
        return new DistinctSequenceSet<TResult>(() => Project(source,selector));
    }

    /// <summary>各要素から得た列を平らにした集合。重複は除く。</summary>
    public static SeamlessSet.ISet<TResult> SelectMany<TSource, TResult>(this SeamlessSet.ISet<TSource> source,Func<TSource,IEnumerable<TResult>> selector) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(selector);
        return new DistinctSequenceSet<TResult>(() => Flatten(source,selector));
    }

    /// <summary>各要素と、その要素から得た列の要素との組を写した集合。重複は除く。</summary>
    public static SeamlessSet.ISet<TResult> SelectMany<TSource, TCollection, TResult>(this SeamlessSet.ISet<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new DistinctSequenceSet<TResult>(() => Flatten(source,collectionSelector,resultSelector));
    }

    /// <summary>キーが等しい要素の組を写した集合。重複は除く。</summary>
    public static SeamlessSet.ISet<TResult> Join<TOuter, TInner, TKey, TResult>(this SeamlessSet.ISet<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector) {
        RequireSet(outer,nameof(outer));
        RequireSet(inner,nameof(inner));
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new DistinctSequenceSet<TResult>(() => JoinRows(outer,inner,outerKeySelector,innerKeySelector,resultSelector));
    }

    /// <summary>
    /// 左外部結合。外側の要素ごとに、キーが等しい内側の要素との組を写す。等しい内側の要素が無い外側の要素は、内側を既定値(参照型・Nullable なら null)にして写す。
    /// null のキーはどの要素とも結び付けない(System.Linq.Enumerable.LeftJoin と同じ)。結果の重複は除く。
    /// 内側を構造体のまま渡すと「対応が無い」が既定値の構造体になり区別できないので、構造体は Nullable にして渡す。
    /// </summary>
    /// <remarks>2026-11-15 利用者の承認(T-SQL 翻訳器の左外部結合。Document/refactoring-designs/20261115-T-SQL翻訳器.md)。</remarks>
    public static SeamlessSet.ISet<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this SeamlessSet.ISet<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector) {
        RequireSet(outer,nameof(outer));
        RequireSet(inner,nameof(inner));
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new DistinctSequenceSet<TResult>(() => LeftJoinRows(outer,inner,outerKeySelector,innerKeySelector,resultSelector));
    }

    /// <summary>
    /// 要素が1つだけの集合。元の集合が空でも1行を返す計算(GROUP BY の無い集約など)の起点に使う。
    /// </summary>
    /// <remarks>2026-11-15 利用者の承認(T-SQL 翻訳器の、GROUP BY の無い集約のビュー)。</remarks>
    public static SeamlessSet.ISet<T> Of<T>(T value) => new SingleSet<T>(value);

    /// <summary>どちらかに含まれる要素の集合。両方に含まれる要素は1回だけ列挙する。</summary>
    public static SeamlessSet.ISet<TSource> Union<TSource>(this SeamlessSet.ISet<TSource> first,SeamlessSet.ISet<TSource> second) {
        RequireSet(first,nameof(first));
        RequireSet(second,nameof(second));
        return new UnionSet<TSource>(first,second);
    }

    /// <summary>両方に含まれる要素の集合。</summary>
    public static SeamlessSet.ISet<TSource> Intersect<TSource>(this SeamlessSet.ISet<TSource> first,SeamlessSet.ISet<TSource> second) {
        RequireSet(first,nameof(first));
        RequireSet(second,nameof(second));
        return new FilteredSet<TSource>(first,second,keepContained: true);
    }

    /// <summary>1つ目にだけ含まれる要素の集合。</summary>
    public static SeamlessSet.ISet<TSource> Except<TSource>(this SeamlessSet.ISet<TSource> first,SeamlessSet.ISet<TSource> second) {
        RequireSet(first,nameof(first));
        RequireSet(second,nameof(second));
        return new FilteredSet<TSource>(first,second,keepContained: false);
    }

    /// <summary>集合は重複を持たないため、元の集合をそのまま返す。</summary>
    public static SeamlessSet.ISet<TSource> Distinct<TSource>(this SeamlessSet.ISet<TSource> source) {
        RequireSet(source,nameof(source));
        return source;
    }

    // 順序なし列を未注釈の ThrowIfNull(object) へ渡すと SEAMLINQ002 になるため、注釈付きの補助で検査する。
    private static void RequireSet<TSequence>([UnorderedEnumerable] TSequence? value,string name) where TSequence : class {
        if(value is null) {
            throw new ArgumentNullException(name);
        }
    }

    private static IEnumerable<TResult> Project<TSource, TResult>(SeamlessSet.ISet<TSource> source,Func<TSource,TResult> selector) {
        foreach(var item in source) {
            yield return selector(item);
        }
    }

    private static IEnumerable<TResult> Flatten<TSource, TResult>(SeamlessSet.ISet<TSource> source,Func<TSource,IEnumerable<TResult>> selector) {
        foreach(var item in source) {
            foreach(var result in selector(item)) {
                yield return result;
            }
        }
    }

    private static IEnumerable<TResult> Flatten<TSource, TCollection, TResult>(SeamlessSet.ISet<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        foreach(var item in source) {
            foreach(var element in collectionSelector(item)) {
                yield return resultSelector(item,element);
            }
        }
    }

    private static IEnumerable<TResult> JoinRows<TOuter, TInner, TKey, TResult>(SeamlessSet.ISet<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector) {
        // System.Linq.Enumerable.Join と同じく、null キーはどの行とも結び付けない。
        var lookup = new Dictionary<JoinKey<TKey>,List<TInner>>();
        foreach(var row in inner) {
            var key = innerKeySelector(row);
            if(key is null) {
                continue;
            }
            if(!lookup.TryGetValue(new(key),out var rows)) {
                rows=[];
                lookup.Add(new(key),rows);
            }
            rows.Add(row);
        }
        foreach(var item in outer) {
            var key = outerKeySelector(item);
            if(key is null||!lookup.TryGetValue(new(key),out var rows)) {
                continue;
            }
            foreach(var row in rows) {
                yield return resultSelector(item,row);
            }
        }
    }

    private static IEnumerable<TResult> LeftJoinRows<TOuter, TInner, TKey, TResult>(SeamlessSet.ISet<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector) {
        var lookup = new Dictionary<JoinKey<TKey>,List<TInner>>();
        foreach(var row in inner) {
            var key = innerKeySelector(row);
            if(key is null) {
                continue;
            }
            if(!lookup.TryGetValue(new(key),out var rows)) {
                rows=[];
                lookup.Add(new(key),rows);
            }
            rows.Add(row);
        }
        foreach(var item in outer) {
            var key = outerKeySelector(item);
            if(key is null||!lookup.TryGetValue(new(key),out var rows)) {
                yield return resultSelector(item,default);
                continue;
            }
            foreach(var row in rows) {
                yield return resultSelector(item,row);
            }
        }
    }

    // Dictionary のキーは null を許さない制約があるため、null を除いたキーを値型で包む。
    private readonly record struct JoinKey<TKey>(TKey Value);

    private abstract class ComputedSet<T>:SeamlessSet.ISet<T> {
        public int Count {
            get {
                var count = this.GetLongCount();
                return count>int.MaxValue ? throw new OverflowException("集合の要素数が Int32 の範囲を超えています。GetLongCount を使ってください。") : (int)count;
            }
        }

        public long GetLongCount() {
            long count = 0;
            using var enumerator = this.GetEnumerator();
            while(enumerator.MoveNext()) {
                count++;
            }
            return count;
        }

        public abstract bool Contains(T item);
        public abstract IEnumerator<T> GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    private sealed class WhereSet<T>(SeamlessSet.ISet<T> source,Func<T,bool> predicate):ComputedSet<T> {
        public override bool Contains(T item) => source.Contains(item)&&predicate(item);

        public override IEnumerator<T> GetEnumerator() {
            foreach(var item in source) {
                if(predicate(item)) {
                    yield return item;
                }
            }
        }
    }

    private sealed class DistinctSequenceSet<T>(Func<IEnumerable<T>> sequence):ComputedSet<T> {
        public override bool Contains(T item) {
            var comparer = EqualityComparer<T>.Default;
            foreach(var candidate in sequence()) {
                if(comparer.Equals(candidate,item)) {
                    return true;
                }
            }
            return false;
        }

        public override IEnumerator<T> GetEnumerator() {
            var seen = new System.Collections.Generic.HashSet<T>();
            foreach(var item in sequence()) {
                if(seen.Add(item)) {
                    yield return item;
                }
            }
        }
    }

    private sealed class SingleSet<T>(T value):ComputedSet<T> {
        public override bool Contains(T item) => EqualityComparer<T>.Default.Equals(value,item);

        public override IEnumerator<T> GetEnumerator() {
            yield return value;
        }
    }

    private sealed class UnionSet<T>(SeamlessSet.ISet<T> first,SeamlessSet.ISet<T> second):ComputedSet<T> {
        public override bool Contains(T item) => first.Contains(item)||second.Contains(item);

        // 1つ目は集合なのでそのまま返し、2つ目は1つ目に含まれない要素だけを返す。メモリ上の重複表は持たない。
        public override IEnumerator<T> GetEnumerator() {
            foreach(var item in first) {
                yield return item;
            }
            foreach(var item in second) {
                if(!first.Contains(item)) {
                    yield return item;
                }
            }
        }
    }

    private sealed class FilteredSet<T>(SeamlessSet.ISet<T> first,SeamlessSet.ISet<T> second,bool keepContained):ComputedSet<T> {
        public override bool Contains(T item) => first.Contains(item)&&second.Contains(item)==keepContained;

        public override IEnumerator<T> GetEnumerator() {
            foreach(var item in first) {
                if(second.Contains(item)==keepContained) {
                    yield return item;
                }
            }
        }
    }
}
