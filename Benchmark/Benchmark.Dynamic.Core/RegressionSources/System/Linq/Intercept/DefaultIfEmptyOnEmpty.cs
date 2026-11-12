namespace Test.System.Linq.Intercept;

// 空の入力に DefaultIfEmpty(既定値) を付け、そのあとに Reverse/TakeLast/SkipLast/Chunk が続く連鎖。
// 2026-09-30 の差分ファズで、既定値を 1 つ返したあとに DefaultIfEmpty の TryGetSpan の答えが変わり(false → 空の span)、
// 最初に「span 経路でない」と決めた消費側(Reverse など)が引き直して空の span を読み、ArgumentOutOfRangeException になるのを確認して直した。
public sealed class DefaultIfEmptyOnEmpty {
    [Fact, Trait("Category","Unit")]
    public void EmptySource_ThenBufferingStage_MatchesSystemLinq() {
        foreach(var source in new[] { new List<int>(),new List<int> { 1,2,3 } }) {
            Assert.Equal(Reverse_NoOptimized(source),Reverse_Optimized(source));
            Assert.Equal(TakeLast_NoOptimized(source),TakeLast_Optimized(source));
            Assert.Equal(SkipLast_NoOptimized(source),SkipLast_Optimized(source));
            Assert.Equal(ChunkSum_NoOptimized(source),ChunkSum_Optimized(source));
            Assert.Equal(ReverseSelectToHashSet_NoOptimized(source),ReverseSelectToHashSet_Optimized(source));
        }
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> Reverse_Optimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).Reverse().ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> Reverse_NoOptimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).Reverse().ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeLast_Optimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).TakeLast(2).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> TakeLast_NoOptimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).TakeLast(2).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SkipLast_Optimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).SkipLast(0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> SkipLast_NoOptimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).SkipLast(0).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ChunkSum_Optimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).Chunk(2).Select(c => c.Sum()).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> ChunkSum_NoOptimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).Chunk(2).Select(c => c.Sum()).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int ReverseSelectToHashSet_Optimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).Reverse().Select(x => x*3).ToHashSet().Count;

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int ReverseSelectToHashSet_NoOptimized(List<int> source) => source.AsEnumerable().DefaultIfEmpty(-1).Reverse().Select(x => x*3).ToHashSet().Count;
}
