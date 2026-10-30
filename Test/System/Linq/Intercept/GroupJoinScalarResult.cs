namespace Test.System.Linq.Intercept;

/// <summary>
/// GroupJoin の結果選択子がグループに集約(Count など)をかける形は、生成コードで結果の変数を二重に宣言すると CS0136 でコンパイルできない。
/// 各テストの式は [LinqIntercept(true)] を付けた private static のメソッドへ置く(付けないと FLinq ではなく System.Linq が実行される)。
/// </summary>
public sealed class GroupJoinScalarResult {
    private static readonly int[] Outer = [1,2,2,3,5];
    private static readonly int[] Inner = [2,2,3,4];

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int GroupJoinCountSumImpl() => Outer.GroupJoin(Inner,static o => o,static i => i,static (o,g) => g.Count()).Sum();
    [Fact, Trait("Category","Unit")]
    public void GroupJoinCountThenSumCompilesAndMatches() => Assert.Equal(5,GroupJoinCountSumImpl());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] GroupJoinSumToArrayImpl() => Outer.GroupJoin(Inner,static o => o,static i => i,static (o,g) => g.Sum()).ToArray();
    [Fact, Trait("Category","Unit")]
    public void GroupJoinSumToArrayMatches() => Assert.Equal([0,4,4,3,0],GroupJoinSumToArrayImpl());
}
