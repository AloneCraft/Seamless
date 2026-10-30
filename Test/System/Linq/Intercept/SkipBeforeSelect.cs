namespace Test.System.Linq.Intercept;

/// <summary>
/// System.Linq は、配列・List・GroupBy の結果などを Select した後の Skip で、飛ばす要素の選択子を呼ばない。
/// 選択子が 0 除算になる要素を飛ばす形で、FLinq も同じ結果になる(以前は、融合したループが飛ばす要素にも選択子を呼んで例外になった)。
/// IEnumerable などでは System.Linq は飛ばす要素にも選択子を呼ぶが、FLinq は呼ばない(System.Linq 以下の評価、という方針)。
/// Select の後の Any は、先頭の要素の選択子も使わない。
/// 各テストの式は [LinqIntercept(true)] を付けた private static のメソッドへ置く(付けないと FLinq ではなく System.Linq が実行される)。
/// </summary>
public sealed class SkipBeforeSelect {
    private static IEnumerable<int> Lazy(params int[] values) {
        foreach(var value in values) {
            yield return value;
        }
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ArraySelectSkipTakeConsumeImpl() {
        var sink = new Consumer<int>();
        new[] { 1,2,3 }.Select(static v => 10/(v-1)).Skip(1).Take(2).Consume(sink);
        return sink.Items.ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void ArraySelectSkipTakeConsumeDoesNotEvaluateSkippedElement() => Assert.Equal([10,5],ArraySelectSkipTakeConsumeImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ListSelectSkipToArrayImpl() => new List<int> { 1,2,3 }.Select(static v => 10/(v-1)).Skip(1).ToArray();
    [Fact, Trait("Category","Unit")]
    public void ListSelectSkipToArrayDoesNotEvaluateSkippedElement() => Assert.Equal([10,5],ListSelectSkipToArrayImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] EnumerableSelectSkipImpl() => Lazy(1,2,3).Select(static v => 10/(v-1)).Skip(1).ToArray();
    [Fact, Trait("Category","Unit")]
    public void EnumerableSelectSkipDoesNotEvaluateSkippedElement() => Assert.Equal([10,5],EnumerableSelectSkipImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ArrayWhereSelectSkipImpl() => new[] { 1,2,3 }.Where(static v => true).Select(static v => 10/(v-1)).Skip(1).ToArray();
    [Fact, Trait("Category","Unit")]
    public void ArrayWhereSelectSkipDoesNotEvaluateSkippedElement() => Assert.Equal([10,5],ArrayWhereSelectSkipImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] GroupBySelectSkipImpl() => new[] { 1,2,3,2 }.GroupBy(static v => v).Select(static g => 10/(g.Key-1)).Skip(1).ToArray();
    [Fact, Trait("Category","Unit")]
    public void GroupBySelectSkipDoesNotEvaluateSkippedGroup() => Assert.Equal([10,5],GroupBySelectSkipImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool SelectAnyImpl() => new[] { 1,2,3 }.Select(static v => 10/(v-1)).Any();
    [Fact, Trait("Category","Unit")]
    public void SelectAnyDoesNotNeedTheSelector() => Assert.True(SelectAnyImpl());
}
