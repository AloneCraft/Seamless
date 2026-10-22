namespace Test.System.Linq.Intercept;

/// <summary>
/// Take(n) は n 個目を通したら止まる。System.Linq は n+1 個目の要素に対して、上流の Select / Where のラムダを呼ばない。
/// 呼ばれると、0 で割るような式が例外になる(2026-10-22 に TPC-E の測定で見つかった)。
/// </summary>
public sealed class TakeStopsUpstream {
    private static IEnumerable<int> Lazy(params int[] values) {
        foreach(var value in values) {
            yield return value;
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ArraySelectTakeToArray() =>
        Assert.Equal([-5,-10],new[] { 1,2,3 }.Select(static v => 10/(v-3)).Take(2).ToArray());

    [Fact, Trait("Category","Unit")]
    public void ArrayWhereTakeToArray() =>
        Assert.Equal([4,5],new[] { 4,5,3 }.Where(static v => 10/(v-3)>0).Take(2).ToArray());

    [Fact, Trait("Category","Unit")]
    public void ListSelectTakeToArray() =>
        Assert.Equal([-5,-10],new List<int> { 1,2,3 }.Select(static v => 10/(v-3)).Take(2).ToArray());

    [Fact, Trait("Category","Unit")]
    public void ListWhereTakeToList() =>
        Assert.Equal([4,5],new List<int> { 4,5,3 }.Where(static v => 10/(v-3)>0).Take(2).ToList());

    [Fact, Trait("Category","Unit")]
    public void EnumerableSelectTakeToArray() =>
        Assert.Equal([-5,-10],Lazy(1,2,3).Select(static v => 10/(v-3)).Take(2).ToArray());

    [Fact, Trait("Category","Unit")]
    public void EnumerableWhereTakeToList() =>
        Assert.Equal([4,5],Lazy(4,5,3).Where(static v => 10/(v-3)>0).Take(2).ToList());

    [Fact, Trait("Category","Unit")]
    public void ArraySelectTakeCount() =>
        Assert.Equal(2,new[] { 1,2,3 }.Select(static v => 10/(v-3)).Take(2).Count());

    [Fact, Trait("Category","Unit")]
    public void ArraySelectTakeSum() =>
        Assert.Equal(-15,new[] { 1,2,3 }.Select(static v => 10/(v-3)).Take(2).Sum());

    [Fact, Trait("Category","Unit")]
    public void ArrayWhereSelectTakeToArray() =>
        Assert.Equal([10,5],new[] { 4,5,3 }.Where(static v => v>0).Select(static v => 10/(v-3)).Take(2).ToArray());
}
