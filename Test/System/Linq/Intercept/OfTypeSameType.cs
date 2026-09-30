namespace Test.System.Linq.Intercept;

// 入力の要素型と同じ型で OfType する連鎖が、コンパイルできるコードになること。
// 2026-09-30 の差分ファズで、値型(int)の入力に OfType<int>() を付けると
// `x is not int y` が「絶対に一致しない」パターン(CS8518)になり、ビルドが通らなくなるのを確認して直した。
public sealed class OfTypeSameType {
    private static List<int> Source() => [9,-2,4,4,0,7,-2,12,3,5,6,1,8];

    [Fact, Trait("Category","Unit")]
    public void OfType_SameValueType_CompilesAndMatchesSystemLinq() {
        Assert.Equal(Sum_NoOptimized(Source()),Sum_Optimized(Source()));
        Assert.Equal(WhereSelectCount_NoOptimized(Source()),WhereSelectCount_Optimized(Source()));
        Assert.Equal(OrderedList_NoOptimized(Source()),OrderedList_Optimized(Source()));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int Sum_Optimized(List<int> source) => source.OfType<int>().Sum();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int Sum_NoOptimized(List<int> source) => source.OfType<int>().Sum();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int WhereSelectCount_Optimized(List<int> source) => source.Where(x => x>0).OfType<int>().Select(x => x+1).Count();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int WhereSelectCount_NoOptimized(List<int> source) => source.Where(x => x>0).OfType<int>().Select(x => x+1).Count();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> OrderedList_Optimized(List<int> source) => source.OfType<int>().OrderBy(x => x).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> OrderedList_NoOptimized(List<int> source) => source.OfType<int>().OrderBy(x => x).ToList();
}
