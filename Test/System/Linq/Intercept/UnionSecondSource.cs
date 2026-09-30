namespace Test.System.Linq.Intercept;

// Union/UnionBy の列挙子は、first を使い切ってから second の未出要素を順に返す。
// 2026-09-30 の差分ファズで、second の最初の未出要素だけを返して残りを落とす不具合を確認して直した
// (Union の直後に Max/Average/Prepend/Append などが続き、Union が単独の融合ループにならない連鎖で起きた)。
public sealed class UnionSecondSource {
    private static List<int> First() => [8,2,4,1,9,2];
    private static List<int> Second() => [1,1,3,40,41,3];

    [Fact, Trait("Category","Unit")]
    public void Union_FollowedByAggregate_KeepsEverySecondSourceElement() {
        Assert.Equal(UnionMax_NoOptimized(First(),Second()),UnionMax_Optimized(First(),Second()));
        Assert.Equal(UnionAverage_NoOptimized(First(),Second()),UnionAverage_Optimized(First(),Second()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int UnionMax_Optimized(List<int> first,List<int> second) => first.Union(second).Max();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int UnionMax_NoOptimized(List<int> first,List<int> second) => first.Union(second).Max();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static double UnionAverage_Optimized(List<int> first,List<int> second) => first.Union(second).Average();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static double UnionAverage_NoOptimized(List<int> first,List<int> second) => first.Union(second).Average();

    [Fact, Trait("Category","Unit")]
    public void Union_FollowedByPrependOrAppend_KeepsEverySecondSourceElement() {
        Assert.Equal(UnionPrepend_NoOptimized(First(),Second()),UnionPrepend_Optimized(First(),Second()));
        Assert.Equal(UnionAppendWhereSelect_NoOptimized(First(),Second()),UnionAppendWhereSelect_Optimized(First(),Second()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> UnionPrepend_Optimized(List<int> first,List<int> second) => first.Union(second).Prepend(4).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> UnionPrepend_NoOptimized(List<int> first,List<int> second) => first.Union(second).Prepend(4).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> UnionAppendWhereSelect_Optimized(List<int> first,List<int> second) => first.Union(second).Where(x => x>1).Select(x => x+1).Append(7).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> UnionAppendWhereSelect_NoOptimized(List<int> first,List<int> second) => first.Union(second).Where(x => x>1).Select(x => x+1).Append(7).ToList();

    [Fact, Trait("Category","Unit")]
    public void UnionBy_FollowedByPrependOrAppend_KeepsEverySecondSourceElement() {
        Assert.Equal(UnionByKey_NoOptimized(First(),Second()),UnionByKey_Optimized(First(),Second()));
        Assert.Equal(UnionByIdentity_NoOptimized(First(),Second()),UnionByIdentity_Optimized(First(),Second()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> UnionByKey_Optimized(List<int> first,List<int> second) => first.UnionBy(second,x => x%7).Prepend(4).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> UnionByKey_NoOptimized(List<int> first,List<int> second) => first.UnionBy(second,x => x%7).Prepend(4).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> UnionByIdentity_Optimized(List<int> first,List<int> second) => first.UnionBy(second,x => x).Append(4).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> UnionByIdentity_NoOptimized(List<int> first,List<int> second) => first.UnionBy(second,x => x).Append(4).ToList();

    [Fact, Trait("Category","Unit")]
    public void UnionBy_FollowedByDistinctBy_CompilesAndMatchesSystemLinq() {
        // second 側の key 変数が次の段(DistinctBy)の key 変数と同じ名前になり、CS0128(重複定義)でビルドできなかった。
        Assert.Equal(UnionByDistinctBy_NoOptimized(First(),Second()),UnionByDistinctBy_Optimized(First(),Second()));
        Assert.Equal(ExceptByDistinctBy_NoOptimized(First(),Second()),ExceptByDistinctBy_Optimized(First(),Second()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> UnionByDistinctBy_Optimized(List<int> first,List<int> second) => first.UnionBy(second,x => x%5).DistinctBy(x => x%4).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> UnionByDistinctBy_NoOptimized(List<int> first,List<int> second) => first.UnionBy(second,x => x%5).DistinctBy(x => x%4).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ExceptByDistinctBy_Optimized(List<int> first,List<int> second) => first.ExceptBy(second,x => x%5).DistinctBy(x => x%4).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> ExceptByDistinctBy_NoOptimized(List<int> first,List<int> second) => first.ExceptBy(second,x => x%5).DistinctBy(x => x%4).ToList();
}
