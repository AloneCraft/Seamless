namespace Test.System.Linq.Intercept;

/// <summary>
/// OrderBy の後の Take / Skip を、Count / Any だけが使うとき、System.Linq は並べ替えの鍵を評価せずに答える。
/// 鍵のラムダが 0 除算になる要素があっても例外にならず、FLinq も同じ結果になる(以前は、鍵を全要素で評価して例外になった)。
/// 各テストの式は [LinqIntercept(true)] を付けた private static のメソッドへ置く。
/// </summary>
public sealed class OrderBeforeCount {
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int OrderTakeCountImpl() => new[] { 1,2,3,4 }.OrderBy(static v => 10/(v-2)).Take(3).Count();
    [Fact, Trait("Category","Unit")]
    public void OrderByTakeCountDoesNotEvaluateTheKey() => Assert.Equal(3,OrderTakeCountImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool OrderSkipAnyImpl() => new[] { 1,2,3,4 }.OrderByDescending(static v => 10/(v-2)).Skip(1).Any();
    [Fact, Trait("Category","Unit")]
    public void OrderByDescendingSkipAnyDoesNotEvaluateTheKey() => Assert.True(OrderSkipAnyImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int OrderWhereTakeCountImpl() => new[] { 4,3,2,1 }.OrderBy(static v => v).Take(2).Where(static v => v>2).Count();
    [Fact, Trait("Category","Unit")]
    public void WhereAfterTakeKeepsTheOrderBecauseTheCountDependsOnIt() => Assert.Equal(0,OrderWhereTakeCountImpl());
}
