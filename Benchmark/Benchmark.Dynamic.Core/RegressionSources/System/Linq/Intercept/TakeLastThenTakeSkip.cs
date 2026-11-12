namespace Test.System.Linq.Intercept;

// TakeLast/SkipLast の後ろに Take/Skip が続く連鎖。
// 2026-09-30 の差分ファズで、Take/Skip が前の段(TakeLast/SkipLast)の件数を読んでしまう不具合を確認して直した
// (TakeLast(6).Take(2) が 6 件返る、TakeLast(6).Skip(1) が空になる)。
// 件数の引数は Take/Skip/SkipLast/TakeLast の順に並ぶのに、Take/Skip の序数は Take/Skip だけを数えていた。
public sealed class TakeLastThenTakeSkip {
    private static List<int> Source() => [9,-2,4,4,0,7,-2,12,3,5,6,1,8];

    [Fact, Trait("Category","Unit")]
    public void TakeLast_ThenTakeOrSkip_UsesEachStagesOwnCount() {
        Assert.Equal(TakeLastSkip_NoOptimized(Source()),TakeLastSkip_Optimized(Source()));
        Assert.Equal(TakeLastTake_NoOptimized(Source()),TakeLastTake_Optimized(Source()));
        Assert.Equal(TakeLastTakeZero_NoOptimized(Source()),TakeLastTakeZero_Optimized(Source()));
        Assert.Equal(TakeLastSkipTake_NoOptimized(Source()),TakeLastSkipTake_Optimized(Source()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeLastSkip_Optimized(List<int> source) => source.TakeLast(6).Skip(1).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TakeLastSkip_NoOptimized(List<int> source) => source.TakeLast(6).Skip(1).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeLastTake_Optimized(List<int> source) => source.TakeLast(6).Take(2).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TakeLastTake_NoOptimized(List<int> source) => source.TakeLast(6).Take(2).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeLastTakeZero_Optimized(List<int> source) => source.TakeLast(1).Take(0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TakeLastTakeZero_NoOptimized(List<int> source) => source.TakeLast(1).Take(0).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeLastSkipTake_Optimized(List<int> source) => source.Skip(2).TakeLast(6).Skip(1).Take(3).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TakeLastSkipTake_NoOptimized(List<int> source) => source.Skip(2).TakeLast(6).Skip(1).Take(3).ToList();

    [Fact, Trait("Category","Unit")]
    public void SkipLast_ThenTakeOrSkip_UsesEachStagesOwnCount() {
        Assert.Equal(SkipLastSkipLastTake_NoOptimized(Source()),SkipLastSkipLastTake_Optimized(Source()));
        Assert.Equal(SkipSkipLastTakeSkip_NoOptimized(Source()),SkipSkipLastTakeSkip_Optimized(Source()));
        Assert.Equal(TakeLastTakeSkipLast_NoOptimized(Source()),TakeLastTakeSkipLast_Optimized(Source()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SkipLastSkipLastTake_Optimized(List<int> source) => source.SkipLast(2).Skip(1).SkipLast(1).Take(4).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> SkipLastSkipLastTake_NoOptimized(List<int> source) => source.SkipLast(2).Skip(1).SkipLast(1).Take(4).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SkipSkipLastTakeSkip_Optimized(List<int> source) => source.Skip(1).SkipLast(2).Take(3).Skip(1).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> SkipSkipLastTakeSkip_NoOptimized(List<int> source) => source.Skip(1).SkipLast(2).Take(3).Skip(1).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeLastTakeSkipLast_Optimized(List<int> source) => source.TakeLast(5).Take(3).SkipLast(1).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TakeLastTakeSkipLast_NoOptimized(List<int> source) => source.TakeLast(5).Take(3).SkipLast(1).ToList();
}
