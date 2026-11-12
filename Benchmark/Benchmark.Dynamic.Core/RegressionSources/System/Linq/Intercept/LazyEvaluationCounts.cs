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
    private static int[] LeftJoinEmptyOuterImpl() {
        calls=0;
        return Array.Empty<int>().LeftJoin(new[] { 1,2,3 },static o => o,static i => Count(i),static (o,i) => o+i).ToArray();
    }
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] RightJoinEmptyInnerImpl() {
        calls=0;
        return new[] { 1,2,3 }.RightJoin(Array.Empty<int>(),static o => Count(o),static i => i,static (o,i) => o+i).ToArray();
    }
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] LeftJoinImpl() {
        calls=0;
        return new[] { 1,2 }.LeftJoin(new[] { 2,3 },static o => o,static i => Count(i),static (o,i) => o*10+i).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void LeftAndRightJoinBuildTheLookupOnlyAfterTheFirstElement() {
        Assert.Empty(LeftJoinEmptyOuterImpl());
        Assert.Equal(0,calls);
        Assert.Empty(RightJoinEmptyInnerImpl());
        Assert.Equal(0,calls);
        Assert.Equal([10,22],LeftJoinImpl());
        Assert.Equal(2,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] JoinFilteredOuterImpl(int[] outer) {
        calls=0;
        return outer.Where(static o => o>1).Join(new[] { 1,2,3 },static o => o,static i => Count(i),static (o,i) => o*10+i).ToArray();
    }
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int JoinFilteredOuterFirstImpl(int[] outer) {
        calls=0;
        return outer.Where(static o => o>1).Join(new[] { 1,2,3 },static o => o,static i => Count(i),static (o,i) => o*10+i).First();
    }
    [Fact, Trait("Category","Unit")]
    public void JoinWithFilteredOuterBuildsTheLookupOnlyAfterTheFirstMatch() {
        Assert.Empty(JoinFilteredOuterImpl([0,1]));
        Assert.Equal(0,calls);
        Assert.Equal([22,33],JoinFilteredOuterImpl([1,2,3]));
        Assert.Equal(3,calls);
        Assert.Equal(22,JoinFilteredOuterFirstImpl([1,2,3]));
        Assert.Equal(3,calls);
        Assert.Throws<InvalidOperationException>(() => JoinFilteredOuterFirstImpl([0,1]));
        Assert.Equal(0,calls);
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
    private static int[] GroupByRuntimeTakeImpl(int take) {
        calls=0;
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v).Select(static g => g.Key*10+g.Sum(static v => Count(v))).Take(take).ToArray();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByRuntimeTakeAggregatesOnlyTheTakenGroups() {
        Assert.Equal([12,24],GroupByRuntimeTakeImpl(2));
        Assert.Equal(4,calls);
        Assert.Empty(GroupByRuntimeTakeImpl(-1));
        Assert.Equal(0,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupByRuntimeTakeEnumeratorImpl(int take) {
        calls=0;
        // Zip は左の列を列挙子で読むので、集約の上限は列挙子の中(Take の値のフィールド)で描かれる。
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v).Select(static g => g.Key*10+g.Sum(static v => Count(v))).Take(take)
            .Zip(new[] { 1,1,1,1 },static (a,b) => a*b).Sum();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByRuntimeTakeThroughTheEnumeratorAggregatesOnlyTheTakenGroups() {
        Assert.Equal(12+24+36,GroupByRuntimeTakeEnumeratorImpl(3));
        Assert.Equal(6,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupByWhereFirstImpl() {
        calls=0;
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v).Select(static g => g.Key*10+g.Sum(static v => Count(v))).Where(static v => v>20).First();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByWhereFirstAggregatesOnlyUntilTheMatch() {
        Assert.Equal(24,GroupByWhereFirstImpl());
        Assert.Equal(4,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupByLastImpl() {
        calls=0;
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v).Select(static g => g.Key*10+g.Sum(static v => Count(v))).Last();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByLastAggregatesOnlyTheLastGroup() {
        Assert.Equal(44,GroupByLastImpl());
        Assert.Equal(1,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupByReverseFirstImpl() {
        calls=0;
        return new[] { 1,2,3,1,2,3,4,4 }.Where(static v => v>1).GroupBy(static v => v%3).Select(static g => g.Key*100+g.Sum(static v => Count(v))).Reverse().FirstOrDefault();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByReverseFirstAggregatesOnlyTheLastGroup() {
        // v>1: 2,3,2,3,4,4。キー v%3 の順は 2,0,1。最後のグループはキー 1(4,4)。
        Assert.Equal(108,GroupByReverseFirstImpl());
        Assert.Equal(2,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupByResultSelectorLastImpl() {
        calls=0;
        return new[] { 1,2,3,1,2,3,4 }.GroupBy(static v => v,static (k,g) => k*10+g.Sum(static v => Count(v))).Last();
    }
    [Fact, Trait("Category","Unit")]
    public void GroupByResultSelectorLastAggregatesOnlyTheLastGroup() {
        Assert.Equal(44,GroupByResultSelectorLastImpl());
        Assert.Equal(1,calls);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupByLastOrDefaultEmptyImpl() => Array.Empty<int>().GroupBy(static v => v).Select(static g => g.Count()).LastOrDefault();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupByLastEmptyImpl() => Array.Empty<int>().GroupBy(static v => v).Select(static g => g.Count()).Last();
    [Fact, Trait("Category","Unit")]
    public void GroupByLastOnEmptySourceMatchesSystemLinq() {
        Assert.Equal(0,GroupByLastOrDefaultEmptyImpl());
        Assert.Throws<InvalidOperationException>(() => GroupByLastEmptyImpl());
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int TakeSelectLastImpl() {
        calls=0;
        return new[] { 1,2,3,4,5,6,7 }.Take(5).Select(static v => Count(v)*10).Last();
    }
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int WhereTakeSelectLastOrDefaultImpl(int[] values) {
        calls=0;
        return values.Where(static v => v%2==1).Take(2).Select(static v => Count(v)*10).LastOrDefault();
    }
    [Fact, Trait("Category","Unit")]
    public void SelectAfterTakeIsEvaluatedOnlyForTheLastElement() {
        Assert.Equal(50,TakeSelectLastImpl());
        Assert.Equal(1,calls);
        Assert.Equal(30,WhereTakeSelectLastOrDefaultImpl([1,2,3,4,5]));
        Assert.Equal(1,calls);
        Assert.Equal(0,WhereTakeSelectLastOrDefaultImpl([2,4]));
        Assert.Equal(0,calls);
    }

    private static readonly (int A,int B,string Name)[] Rows = [(3,1,"a"),(1,5,"b"),(2,0,"c"),(1,2,"d"),(1,2,"e"),(4,9,"f")];

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static string OrderThenFirstImpl() {
        calls=0;
        return Rows.OrderBy(static r => r.A).ThenBy(static r => Count(r.B)).First().Name;
    }
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static string OrderDescendingThenDescendingFirstImpl() {
        calls=0;
        return Rows.OrderByDescending(static r => r.A%3).ThenByDescending(static r => Count(r.B)).Select(static r => r.Name).FirstOrDefault()!;
    }
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static string? OrderThenThenFirstOrDefaultImpl((int A,int B,string Name)[] rows) {
        calls=0;
        return rows.OrderBy(static r => r.A).ThenBy(static r => r.B).ThenBy(static r => r.Name,StringComparer.Ordinal).Select(static r => r.Name).FirstOrDefault();
    }
    [Fact, Trait("Category","Unit")]
    public void OrderThenByFirstEvaluatesSecondaryKeysOnlyOnTies() {
        // A=1 の3件(b,d,e)が同順位。二次の鍵は、最良の要素 b の鍵と、d・e の鍵の3回だけ(System.Linq はもっと多い)。
        Assert.Equal("d",OrderThenFirstImpl());
        Assert.Equal(3,calls);
        // A%3 の降順で最大は 2(c だけ)なので、二次の鍵は評価しない。
        Assert.Equal("c",OrderDescendingThenDescendingFirstImpl());
        Assert.Equal(0,calls);
        Assert.Equal("d",OrderThenThenFirstOrDefaultImpl(Rows));
        Assert.Null(OrderThenThenFirstOrDefaultImpl([]));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool ReverseWhereAnyImpl() {
        calls=0;
        return new[] { 1,2,3,4,5 }.Reverse().Where(static v => Count(v)%2==0).Any();
    }
    [Fact, Trait("Category","Unit")]
    public void ReverseWhereAnyScansFromTheEnd() {
        // System.Linq は後ろ(5)から評価して、4 で見つける(2回)。
        Assert.True(ReverseWhereAnyImpl());
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
