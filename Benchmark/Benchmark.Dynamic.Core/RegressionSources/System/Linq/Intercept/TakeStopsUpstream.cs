namespace Test.System.Linq.Intercept;

/// <summary>
/// Take(n) は n 個目を通したら止まる。System.Linq は n+1 個目の要素に対して、上流の Select / Where のラムダを呼ばない。
/// 呼ばれると、0 で割るような式が例外になる(2026-10-22 に TPC-E の測定で見つかった)。
/// 各テストの式は、[LinqIntercept(true)] を付けた private static のメソッドへ置く(付けないと、FLinq ではなく System.Linq が実行される)。
/// 段の並び・源・終端を網羅した評価回数の比較は TakeStopsUpstreamMatrix にある。
/// </summary>
public sealed class TakeStopsUpstream {
    private static IEnumerable<int> Lazy(params int[] values) {
        foreach(var value in values) {
            yield return value;
        }
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ArraySelectTakeToArrayImpl() => new[] { 1,2,3 }.Select(static v => 10/(v-3)).Take(2).ToArray();
    [Fact, Trait("Category","Unit")]
    public void ArraySelectTakeToArray() => Assert.Equal([-5,-10],ArraySelectTakeToArrayImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ArrayWhereTakeToArrayImpl() => new[] { 4,5,3 }.Where(static v => 10/(v-3)>0).Take(2).ToArray();
    [Fact, Trait("Category","Unit")]
    public void ArrayWhereTakeToArray() => Assert.Equal([4,5],ArrayWhereTakeToArrayImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ListSelectTakeToArrayImpl() => new List<int> { 1,2,3 }.Select(static v => 10/(v-3)).Take(2).ToArray();
    [Fact, Trait("Category","Unit")]
    public void ListSelectTakeToArray() => Assert.Equal([-5,-10],ListSelectTakeToArrayImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ListWhereTakeToListImpl() => new List<int> { 4,5,3 }.Where(static v => 10/(v-3)>0).Take(2).ToList();
    [Fact, Trait("Category","Unit")]
    public void ListWhereTakeToList() => Assert.Equal([4,5],ListWhereTakeToListImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] EnumerableSelectTakeToArrayImpl() => Lazy(1,2,3).Select(static v => 10/(v-3)).Take(2).ToArray();
    [Fact, Trait("Category","Unit")]
    public void EnumerableSelectTakeToArray() => Assert.Equal([-5,-10],EnumerableSelectTakeToArrayImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> EnumerableWhereTakeToListImpl() => Lazy(4,5,3).Where(static v => 10/(v-3)>0).Take(2).ToList();
    [Fact, Trait("Category","Unit")]
    public void EnumerableWhereTakeToList() => Assert.Equal([4,5],EnumerableWhereTakeToListImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int ArraySelectTakeCountImpl() => new[] { 1,2,3 }.Select(static v => 10/(v-3)).Take(2).Count();
    [Fact, Trait("Category","Unit")]
    public void ArraySelectTakeCount() => Assert.Equal(2,ArraySelectTakeCountImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int ArraySelectTakeSumImpl() => new[] { 1,2,3 }.Select(static v => 10/(v-3)).Take(2).Sum();
    [Fact, Trait("Category","Unit")]
    public void ArraySelectTakeSum() => Assert.Equal(-15,ArraySelectTakeSumImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ArrayWhereSelectTakeToArrayImpl() => new[] { 4,5,3 }.Where(static v => v>0).Select(static v => 10/(v-3)).Take(2).ToArray();
    [Fact, Trait("Category","Unit")]
    public void ArrayWhereSelectTakeToArray() => Assert.Equal([10,5],ArrayWhereSelectTakeToArrayImpl());
}
