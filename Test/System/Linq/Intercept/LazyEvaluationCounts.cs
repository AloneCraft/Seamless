namespace Test.System.Linq.Intercept;

/// <summary>
/// FLinq は System.Linq が評価しない要素にラムダを評価しない(評価の回数が System.Linq 以下)。
/// ここでは、Join の内側の探索表を外側の最初の要素まで作らないこと、GroupBy の集約を使うグループの分だけ行うこと、
/// 要素1個の並べ替えで鍵を評価しないこと、Concat の Any が後半を見ないことを、ラムダを呼んだ回数で確かめる。
/// 各テストの式は [LinqIntercept(true)] を付けた private static のメソッドへ置く(付けないと FLinq ではなく System.Linq が実行される)。
/// </summary>
public sealed class LazyEvaluationCounts {
    private static int calls;

    private static int Count(int value) {
        calls++;
        return value;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int JoinEmptyOuterImpl() {
        calls=0;
        var result = Array.Empty<int>().Join(new[] { 1,2,3 },static o => o,static i => Count(i),static (o,i) => o+i).Count();
        return result;
    }
    [Fact, Trait("Category","Unit")]
    public void JoinWithEmptyOuterDoesNotEvaluateInnerKeys() {
        Assert.Equal(0,JoinEmptyOuterImpl());
        Assert.Equal(0,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] JoinEmptyOuterToArrayImpl() {
        calls=0;
        return Array.Empty<int>().Join(new[] { 1,2,3 },static o => o,static i => Count(i),static (o,i) => o+i).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void JoinWithEmptyOuterToArrayDoesNotEvaluateInnerKeys() {
        Assert.Empty(JoinEmptyOuterToArrayImpl());
        Assert.Equal(0,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] JoinNonEmptyOuterImpl() {
        calls=0;
        return new[] { 2,3 }.Join(new[] { 1,2,3 },static o => o,static i => Count(i),static (o,i) => o*10+i).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void JoinWithNonEmptyOuterStillJoins() {
        Assert.Equal([22,33],JoinNonEmptyOuterImpl());
        Assert.Equal(3,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupByFirstImpl() {
        calls=0;
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v).Select(static g => g.Key*10+g.Sum(static v => Count(v))).First();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByFirstAggregatesOnlyTheFirstGroup() {
        Assert.Equal(12,GroupByFirstImpl());
        Assert.Equal(2,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] GroupByTakeImpl() {
        calls=0;
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v).Select(static g => g.Key*10+g.Sum(static v => Count(v))).Take(2).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByTakeAggregatesOnlyTheTakenGroups() {
        Assert.Equal([12,24],GroupByTakeImpl());
        Assert.Equal(4,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] GroupBySkipTakeImpl() {
        calls=0;
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v).Select(static g => g.Key*10+g.Sum(static v => Count(v))).Skip(1).Take(1).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupBySkipTakeAggregatesOnlyTheTakenGroups() {
        Assert.Equal([24],GroupBySkipTakeImpl());
        Assert.Equal(2,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] GroupByAllGroupsImpl() {
        calls=0;
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v).Select(static g => g.Key*10+g.Sum(static v => Count(v))).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByWithoutLimitAggregatesEveryGroup() {
        Assert.Equal([12,24,36,44],GroupByAllGroupsImpl());
        Assert.Equal(7,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] OrderSingleElementImpl() {
        calls=0;
        return new[] { 5 }.OrderBy(static v => Count(v)).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void OrderOfOneElementDoesNotEvaluateKeys() {
        Assert.Equal([5],OrderSingleElementImpl());
        Assert.Equal(0,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool ConcatAnyImpl() {
        calls=0;
        var other = new[] { 7 };
        return new[] { 1,2 }.Select(static v => Count(v)).Concat(other).Any();
    }
    [Fact, Trait("Category","Unit")]
    public void ConcatAnyDoesNotEvaluateSelector() {
        Assert.True(ConcatAnyImpl());
        Assert.Equal(0,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool SelectManyAnyImpl() {
        calls=0;
        var inner = new[] { 1,2,3 };
        return new[] { 10,20 }.SelectMany(o => inner.Select(i => Count(o+i))).Any();
    }
    [Fact, Trait("Category","Unit")]
    public void SelectManyAnyDoesNotEvaluateInnerSelector() {
        Assert.True(SelectManyAnyImpl());
        Assert.Equal(0,calls);
    }
}
