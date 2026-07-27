namespace Shared.Linq;

/*
Tsavorite の HashSet<T>/Persisted の Set<T> は列挙順序を保証しない。Base.cs の *_Impl は
これらへ順序依存演算(Order/Reverse/First/ElementAt/Skip/Take/Zip/SequenceEqual/index付き
Select・Where/Chunk/Concat/Append/Prepend/Index)を直接適用しており、ExecuteOrderDependent
は TSAVORITE||PERSISTED かつ !IENUMERABLE_ROOT のとき NotSupportedException を期待する。

本ファイルは Base.cs / Route.cs と同じ Shared.Linq 名前空間へ同名の拡張メソッドを置くことで、
System.Linq.Enumerable / Seamless.Linq.Enumerable の該当オーバーロードより優先して解決させる
(C# の拡張メソッド解決は「その名前で引数に適用できる候補が見つかった最も内側のスコープ」で
確定し、外側スコープへは進まない。一方シグネチャが一致しない呼び出し、例えば非 index の
Select/Where は本ファイルに同名候補が無いため通常どおり外側スコープへフォールバックする)。

Tsavorite HashSet<T> と Persisted Set<T> はどちらも Seamless.Collections.IUnorderedEnumerable<T>
を実装するため、.Linq/.FLinq ルート(生コレクションをそのまま渡す)向けは1組で両方をカバーする。
.ValueLinq ルートは ValueEnumerable<TEnumerator,T> を経由し、Tsavorite/Persisted で
enumerator 型が異なる(共有マーカーを持たない)ため、2組に分けて定義する。

Seamless.Collections / Seamless.Linq を参照しないフレーバー(ZLinq 比較用など)でも
このファイルは共有プロジェクト経由で無条件にコンパイルされるため、Base.cs 本体の
TSAVORITE/PERSISTED 判定と同じガードで囲む(CS0400: 'Seamless' が見つからない対策)。
*/
#if TSAVORITE || PERSISTED
internal static class UnorderedGuard {
    private static global::System.NotSupportedException Guard(string operation) =>
        new($"{operation} is order-dependent; enumeration order is unspecified for this source.");

    // ---- 生コレクション経由 (.Linq / .FLinq ルート) ----
    public static global::System.Collections.Generic.IEnumerable<T> Append<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,T element) => throw Guard(nameof(Append));
    public static global::System.Collections.Generic.IEnumerable<T[]> Chunk<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,int size) => throw Guard(nameof(Chunk));
    public static global::System.Collections.Generic.IEnumerable<T> Concat<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(Concat));
    public static T ElementAt<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,int index) => throw Guard(nameof(ElementAt));
    public static T? ElementAtOrDefault<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,int index) => throw Guard(nameof(ElementAtOrDefault));
    public static T First<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source) => throw Guard(nameof(First));
    public static T? FirstOrDefault<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source) => throw Guard(nameof(FirstOrDefault));
    public static T? FirstOrDefault<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,bool> predicate) => throw Guard(nameof(FirstOrDefault));
    public static global::System.Collections.Generic.IEnumerable<(int Index,T Item)> Index<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source) => throw Guard(nameof(Index));
    public static T Last<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source) => throw Guard(nameof(Last));
    public static T? LastOrDefault<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source) => throw Guard(nameof(LastOrDefault));
    public static T? LastOrDefault<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,bool> predicate) => throw Guard(nameof(LastOrDefault));
    public static IOrderedEnumerable<T> Order<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source) => throw Guard(nameof(Order));
    public static IOrderedEnumerable<T> OrderBy<T,TKey>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,TKey> keySelector) => throw Guard(nameof(OrderBy));
    public static IOrderedEnumerable<T> OrderByDescending<T,TKey>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,TKey> keySelector) => throw Guard(nameof(OrderByDescending));
    public static IOrderedEnumerable<T> OrderDescending<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source) => throw Guard(nameof(OrderDescending));
    public static global::System.Collections.Generic.IEnumerable<T> Prepend<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,T element) => throw Guard(nameof(Prepend));
    public static global::System.Collections.Generic.IEnumerable<T> Reverse<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source) => throw Guard(nameof(Reverse));
    public static global::System.Collections.Generic.IEnumerable<TResult> Select<T,TResult>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,int,TResult> selector) => throw Guard(nameof(Select));
    public static bool SequenceEqual<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(SequenceEqual));
    public static global::System.Collections.Generic.IEnumerable<T> Skip<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,int count) => throw Guard(nameof(Skip));
    public static global::System.Collections.Generic.IEnumerable<T> SkipLast<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,int count) => throw Guard(nameof(SkipLast));
    public static global::System.Collections.Generic.IEnumerable<T> SkipWhile<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,int,bool> predicate) => throw Guard(nameof(SkipWhile));
    public static global::System.Collections.Generic.IEnumerable<T> SkipWhile<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,bool> predicate) => throw Guard(nameof(SkipWhile));
    public static global::System.Collections.Generic.IEnumerable<T> Take<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,int count) => throw Guard(nameof(Take));
    public static global::System.Collections.Generic.IEnumerable<T> TakeLast<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,int count) => throw Guard(nameof(TakeLast));
    public static global::System.Collections.Generic.IEnumerable<T> TakeWhile<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,int,bool> predicate) => throw Guard(nameof(TakeWhile));
    public static global::System.Collections.Generic.IEnumerable<T> TakeWhile<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,bool> predicate) => throw Guard(nameof(TakeWhile));
    public static global::System.Collections.Generic.IEnumerable<T> Where<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,Func<T,int,bool> predicate) => throw Guard(nameof(Where));
    public static global::System.Collections.Generic.IEnumerable<TResult> Zip<T,TResult>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,global::System.Collections.Generic.IEnumerable<T> second,Func<T,T,TResult> resultSelector) => throw Guard(nameof(Zip));
    public static global::System.Collections.Generic.IEnumerable<(T First,T Second)> Zip<T>(this global::Seamless.Collections.IUnorderedEnumerable<T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(Zip));

    // ---- ValueLinq ルート: Tsavorite HashSet<T> (TsavoriteHashSetEnumerator<T>) ----
    public static global::System.Collections.Generic.IEnumerable<T> Append<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,T element) => throw Guard(nameof(Append));
    public static global::System.Collections.Generic.IEnumerable<T[]> Chunk<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,int size) => throw Guard(nameof(Chunk));
    public static global::System.Collections.Generic.IEnumerable<T> Concat<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(Concat));
    public static T ElementAt<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,int index) => throw Guard(nameof(ElementAt));
    public static T? ElementAtOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,int index) => throw Guard(nameof(ElementAtOrDefault));
    public static T First<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source) => throw Guard(nameof(First));
    public static T? FirstOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source) => throw Guard(nameof(FirstOrDefault));
    public static T? FirstOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,bool> predicate) => throw Guard(nameof(FirstOrDefault));
    public static global::System.Collections.Generic.IEnumerable<(int Index,T Item)> Index<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source) => throw Guard(nameof(Index));
    public static T Last<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source) => throw Guard(nameof(Last));
    public static T? LastOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source) => throw Guard(nameof(LastOrDefault));
    public static T? LastOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,bool> predicate) => throw Guard(nameof(LastOrDefault));
    public static IOrderedEnumerable<T> Order<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source) => throw Guard(nameof(Order));
    public static IOrderedEnumerable<T> OrderBy<T,TKey>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,TKey> keySelector) => throw Guard(nameof(OrderBy));
    public static IOrderedEnumerable<T> OrderByDescending<T,TKey>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,TKey> keySelector) => throw Guard(nameof(OrderByDescending));
    public static IOrderedEnumerable<T> OrderDescending<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source) => throw Guard(nameof(OrderDescending));
    public static global::System.Collections.Generic.IEnumerable<T> Prepend<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,T element) => throw Guard(nameof(Prepend));
    public static global::System.Collections.Generic.IEnumerable<T> Reverse<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source) => throw Guard(nameof(Reverse));
    public static global::System.Collections.Generic.IEnumerable<TResult> Select<T,TResult>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,int,TResult> selector) => throw Guard(nameof(Select));
    public static bool SequenceEqual<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(SequenceEqual));
    public static global::System.Collections.Generic.IEnumerable<T> Skip<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,int count) => throw Guard(nameof(Skip));
    public static global::System.Collections.Generic.IEnumerable<T> SkipLast<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,int count) => throw Guard(nameof(SkipLast));
    public static global::System.Collections.Generic.IEnumerable<T> SkipWhile<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,int,bool> predicate) => throw Guard(nameof(SkipWhile));
    public static global::System.Collections.Generic.IEnumerable<T> SkipWhile<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,bool> predicate) => throw Guard(nameof(SkipWhile));
    public static global::System.Collections.Generic.IEnumerable<T> Take<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,int count) => throw Guard(nameof(Take));
    public static global::System.Collections.Generic.IEnumerable<T> TakeLast<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,int count) => throw Guard(nameof(TakeLast));
    public static global::System.Collections.Generic.IEnumerable<T> TakeWhile<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,int,bool> predicate) => throw Guard(nameof(TakeWhile));
    public static global::System.Collections.Generic.IEnumerable<T> TakeWhile<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,bool> predicate) => throw Guard(nameof(TakeWhile));
    public static global::System.Collections.Generic.IEnumerable<T> Where<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,Func<T,int,bool> predicate) => throw Guard(nameof(Where));
    public static global::System.Collections.Generic.IEnumerable<TResult> Zip<T,TResult>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,global::System.Collections.Generic.IEnumerable<T> second,Func<T,T,TResult> resultSelector) => throw Guard(nameof(Zip));
    public static global::System.Collections.Generic.IEnumerable<(T First,T Second)> Zip<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.TsavoriteHashSetEnumerator<T>,T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(Zip));

    // ---- ValueLinq ルート: Persisted Set<T> (PersistedSetEnumerator<T>) ----
    public static global::System.Collections.Generic.IEnumerable<T> Append<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,T element) => throw Guard(nameof(Append));
    public static global::System.Collections.Generic.IEnumerable<T[]> Chunk<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,int size) => throw Guard(nameof(Chunk));
    public static global::System.Collections.Generic.IEnumerable<T> Concat<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(Concat));
    public static T ElementAt<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,int index) => throw Guard(nameof(ElementAt));
    public static T? ElementAtOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,int index) => throw Guard(nameof(ElementAtOrDefault));
    public static T First<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source) => throw Guard(nameof(First));
    public static T? FirstOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source) => throw Guard(nameof(FirstOrDefault));
    public static T? FirstOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,bool> predicate) => throw Guard(nameof(FirstOrDefault));
    public static global::System.Collections.Generic.IEnumerable<(int Index,T Item)> Index<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source) => throw Guard(nameof(Index));
    public static T Last<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source) => throw Guard(nameof(Last));
    public static T? LastOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source) => throw Guard(nameof(LastOrDefault));
    public static T? LastOrDefault<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,bool> predicate) => throw Guard(nameof(LastOrDefault));
    public static IOrderedEnumerable<T> Order<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source) => throw Guard(nameof(Order));
    public static IOrderedEnumerable<T> OrderBy<T,TKey>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,TKey> keySelector) => throw Guard(nameof(OrderBy));
    public static IOrderedEnumerable<T> OrderByDescending<T,TKey>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,TKey> keySelector) => throw Guard(nameof(OrderByDescending));
    public static IOrderedEnumerable<T> OrderDescending<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source) => throw Guard(nameof(OrderDescending));
    public static global::System.Collections.Generic.IEnumerable<T> Prepend<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,T element) => throw Guard(nameof(Prepend));
    public static global::System.Collections.Generic.IEnumerable<T> Reverse<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source) => throw Guard(nameof(Reverse));
    public static global::System.Collections.Generic.IEnumerable<TResult> Select<T,TResult>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,int,TResult> selector) => throw Guard(nameof(Select));
    public static bool SequenceEqual<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(SequenceEqual));
    public static global::System.Collections.Generic.IEnumerable<T> Skip<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,int count) => throw Guard(nameof(Skip));
    public static global::System.Collections.Generic.IEnumerable<T> SkipLast<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,int count) => throw Guard(nameof(SkipLast));
    public static global::System.Collections.Generic.IEnumerable<T> SkipWhile<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,int,bool> predicate) => throw Guard(nameof(SkipWhile));
    public static global::System.Collections.Generic.IEnumerable<T> SkipWhile<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,bool> predicate) => throw Guard(nameof(SkipWhile));
    public static global::System.Collections.Generic.IEnumerable<T> Take<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,int count) => throw Guard(nameof(Take));
    public static global::System.Collections.Generic.IEnumerable<T> TakeLast<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,int count) => throw Guard(nameof(TakeLast));
    public static global::System.Collections.Generic.IEnumerable<T> TakeWhile<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,int,bool> predicate) => throw Guard(nameof(TakeWhile));
    public static global::System.Collections.Generic.IEnumerable<T> TakeWhile<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,bool> predicate) => throw Guard(nameof(TakeWhile));
    public static global::System.Collections.Generic.IEnumerable<T> Where<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,Func<T,int,bool> predicate) => throw Guard(nameof(Where));
    public static global::System.Collections.Generic.IEnumerable<TResult> Zip<T,TResult>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,global::System.Collections.Generic.IEnumerable<T> second,Func<T,T,TResult> resultSelector) => throw Guard(nameof(Zip));
    public static global::System.Collections.Generic.IEnumerable<(T First,T Second)> Zip<T>(this global::Seamless.Linq.ValueEnumerable<global::Seamless.Linq.PersistedSetEnumerator<T>,T> source,global::System.Collections.Generic.IEnumerable<T> second) => throw Guard(nameof(Zip));
}
#endif
