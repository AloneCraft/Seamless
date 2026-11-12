using CommonStore=global::Seamless.Execution.Store;
namespace Test.System.Linq.Intercept;

// Chunk の端数の最後のチャンクを返したあとの呼び出しで、source をもう一度読んでしまう不具合。
// 使い切ったあとの MoveNext で最初から(最後のチャンクから)読み直す列挙子(Tsavorite の HashSet)では、チャンクが無限に出続けて止まらなかった
// (2026-09-30 の差分ファズで確認)。Chunk の列挙子が「source を使い切った」旗を持つ + Tsavorite の列挙子が false を返し続けるようにして直した。
public sealed class ChunkPartialLast {
    [Fact, Trait("Category","Unit")]
    public void Chunk_WithPartialLastChunk_MatchesSystemLinq() {
        var source = new List<int> { 3,1,4,1,5 };
        Assert.Equal(ChunkSums_NoOptimized(source),ChunkSums_Optimized(source));
        Assert.Equal(ChunkCount_NoOptimized(source),ChunkCount_Optimized(source));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ChunkSums_Optimized(List<int> source) => source.Where(x => x>0).Chunk(2).Select(c => c.Sum()).ToList();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> ChunkSums_NoOptimized(List<int> source) => source.Where(x => x>0).Chunk(2).Select(c => c.Sum()).ToList();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int ChunkCount_Optimized(List<int> source) => source.Where(x => x>0).Chunk(2).Count();

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int ChunkCount_NoOptimized(List<int> source) => source.Where(x => x>0).Chunk(2).Count();

    [Fact, Trait("Category","Unit")]
    public void Chunk_OverTsavoriteSet_Terminates() {
        // 修正前は Count() が終わらない(チャンクが無限に出る)。別スレッドで走らせて、長く待っても終わらなければ失敗にする。
        // Tsavorite の操作用オブジェクトはスレッドに紐づくので、作成も走らせるスレッドの中で行う。
        var task = Task.Run(() => {
            using var store = CommonStore.CreateMemory();
            using var data = store.CreateTsavoriteHashSet<long>("ids");
            using var set = data.Create();
            set.Add(3);
            return TsavoriteChunkCount_Optimized(set);
        });
        Assert.True(task.Wait(TimeSpan.FromSeconds(20)),"Chunk が終わらない(使い切った列挙子を読み直している)。");
        Assert.Equal(1,task.Result);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int TsavoriteChunkCount_Optimized(global::Seamless.Collections.Tsavorite.HashSet<long> set) => set.Chunk(2).Count();
}
