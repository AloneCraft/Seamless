namespace Test.System.Linq.Intercept;

/// <summary>
/// 「下流がどこまで使うか」の判定の見直し(2026-10-24、docs/refactoring-designs/20261024-2208-flinq-demand-analysis.md)で見つかった、
/// System.Linq と結果・例外が食い違う形。
/// - UnionBy の Consume は、第 2 引数の要素もキー選択子を通して出す(Except/Intersect 系のように、第 2 引数をキーの列として先に読まない)。
/// - GroupJoin は、null キーの内側も外側も結合しない。
/// - 独自の比較子の Distinct は、後ろの集合演算の前で捨てない(どの重複が残るかが比較子で決まる)。
/// - 並べ替えの後の Where + Any は、並べ替えた順に述語を評価する(元の順で評価すると、System.Linq が評価しない要素で例外になる)。
/// 各テストの式は [LinqIntercept(true)] を付けた private static のメソッドへ置く(付けないと FLinq ではなく System.Linq が実行される)。
/// </summary>
public sealed class DemandCorrectness {
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] UnionByConsumeImpl() {
        var sink = new Consumer<int>();
        new[] { 1,2,3 }.UnionBy(new[] { 3,4,13 },static v => v%10).Consume(sink);
        return sink.Items.ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void UnionByConsumeEmitsSecondSource() => Assert.Equal([1,2,3,4],UnionByConsumeImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static (int,string)[] UnionByOtherKeyTypeConsumeImpl() {
        var sink = new Consumer<(int,string)>();
        new[] { (1,"a"),(2,"b") }.UnionBy(new[] { (2,"c"),(3,"d") },static v => v.Item1).Consume(sink);
        return sink.Items.ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void UnionByWithOtherKeyTypeConsumeEmitsSecondSource() => Assert.Equal([(1,"a"),(2,"b"),(3,"d")],UnionByOtherKeyTypeConsumeImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] UnionByToArrayImpl() => new[] { 1,2,3 }.UnionBy(new[] { 3,4,13 },static v => v%10).ToArray();
    [Fact, Trait("Category","Unit")]
    public void UnionByToArrayEmitsSecondSource() => Assert.Equal([1,2,3,4],UnionByToArrayImpl());

    private static readonly string?[] NullOuter = ["a",null,"b"];
    private static readonly string?[] NullInner = [null,"a","a"];

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] GroupJoinNullKeyToArrayImpl() => NullOuter.GroupJoin(NullInner,static o => o,static i => i,static (o,g) => g.Count()).ToArray();
    [Fact, Trait("Category","Unit")]
    public void GroupJoinNullKeysDoNotMatchToArray() => Assert.Equal([2,0,0],GroupJoinNullKeyToArrayImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] GroupJoinNullKeyWhereImpl() => NullOuter.GroupJoin(NullInner,static o => o,static i => i,static (o,g) => g.Count()).Where(static c => c>=0).Take(3).ToArray();
    [Fact, Trait("Category","Unit")]
    public void GroupJoinNullKeysDoNotMatchThroughEnumerator() => Assert.Equal([2,0,0],GroupJoinNullKeyWhereImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupJoinNullKeyFirstImpl() => NullOuter.Skip(1).GroupJoin(NullInner,static o => o,static i => i,static (o,g) => g.Count()).First();
    [Fact, Trait("Category","Unit")]
    public void GroupJoinNullKeysDoNotMatchFirst() => Assert.Equal(0,GroupJoinNullKeyFirstImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] GroupJoinNullKeyZipImpl() => NullOuter.GroupJoin(NullInner,static o => o,static i => i,static (o,g) => g.Count()).Zip(new[] { 10,20,30 },static (c,z) => c+z).ToArray();
    [Fact, Trait("Category","Unit")]
    public void GroupJoinNullKeysDoNotMatchThroughZip() => Assert.Equal([12,20,30],GroupJoinNullKeyZipImpl());

    private static readonly string[] Words = ["a","A","b"];

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static string DistinctComparerExceptFirstImpl() => Words.Distinct(StringComparer.OrdinalIgnoreCase).Except(new[] { "a" }).First();
    [Fact, Trait("Category","Unit")]
    public void DistinctWithComparerIsKeptBeforeExceptFirst() => Assert.Equal("b",DistinctComparerExceptFirstImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool DistinctComparerExceptContainsImpl() => Words.Distinct(StringComparer.OrdinalIgnoreCase).Except(new[] { "a" }).Contains("A");
    [Fact, Trait("Category","Unit")]
    public void DistinctWithComparerIsKeptBeforeExceptContains() => Assert.False(DistinctComparerExceptContainsImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int DistinctComparerExceptCountImpl() => Words.Distinct(StringComparer.OrdinalIgnoreCase).Except(new[] { "a" }).Count();
    [Fact, Trait("Category","Unit")]
    public void DistinctWithComparerIsKeptBeforeExceptCount() => Assert.Equal(1,DistinctComparerExceptCountImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool OrderWhereAnyImpl() => new[] { 3,4 }.OrderByDescending(static x => x).Where(static x => 10/(x-3)>0).Any();
    [Fact(Skip = "段階 6 で直す(並べ替えを消す規則を、要素ごとに System.Linq に合わせる)"), Trait("Category","Unit")]
    public void OrderWhereAnyEvaluatesInSortedOrder() => Assert.True(OrderWhereAnyImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool ReverseWhereAnyImpl() => new[] { 3,4 }.Reverse().Where(static x => 10/(x-3)>0).Any();
    [Fact(Skip = "段階 6 で直す(並べ替えを消す規則を、要素ごとに System.Linq に合わせる)"), Trait("Category","Unit")]
    public void ReverseWhereAnyEvaluatesInReversedOrder() => Assert.True(ReverseWhereAnyImpl());
}
