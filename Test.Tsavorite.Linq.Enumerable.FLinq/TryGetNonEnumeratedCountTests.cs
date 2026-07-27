namespace Test.Tsavorite.Linq.Enumerable.FLinq;

public sealed class TryGetNonEnumeratedCountTests {
    private static global::Seamless.Collections.Tsavorite.HashSet<int>? source;

    // Benchmark.db の実測(実行ID=7)では Tsavorite<T> の TryGetNonEnumeratedCount(単独呼び出し)が
    // FLinq 対 ValueLinq で 7.9〜15.8x 多く割り当てていた(絶対量は 16KB/33KB 対 2KB と小さい)。
    // 調査したところ単なる性能劣化ではなく正しさの退行だった: Tsavorite.HashSet<T> は
    // IMemoryCollection<T>:ICollection<T> を実装しているため、素の System.Linq.Enumerable.
    // TryGetNonEnumeratedCount は true を返せる。ところが FLinq の介入経路(LinqMethodBodyRewriter.cs の
    // TryRewriteTryGetNonEnumeratedCountStatement、Fragment 機構を経由しない専用書き換え)は
    // ProviderNativeRootBase の汎用 struct(Root.cs、useEnumerableInterface:false)の
    // TryGetNonEnumeratedCount を経由し、それは常に false を返す固定実装だった
    // (素の呼び出しより悪い結果を返す退行)。LinqMethodBodyRewriter.cs に
    // ProviderNativeRootBase 用の fast path(NativeCountExpression を直接使う)を追加して解消した。
    [Fact,Trait("Category","Unit")]
    public void TryGetNonEnumeratedCount_AllocatesNoMoreThanRaw() {
        using var store=global::Seamless.Collections.Tsavorite.Store.Create(MemorySize:1L<<24).CreateHashSet<int>();
        using var set=store.Create();
        const int count=2000;
        for(var i=1;i<=count;i++)
            set.Add(i);
        source=set;

        for(var i=0;i<5;i++) {
            _=FLinqTryGetNonEnumeratedCountImpl();
            _=RawTryGetNonEnumeratedCountImpl();
        }

        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        var beforeFlinq=GC.GetAllocatedBytesForCurrentThread();
        var (flinqOk,flinqCount)=FLinqTryGetNonEnumeratedCountImpl();
        var flinqBytes=GC.GetAllocatedBytesForCurrentThread()-beforeFlinq;

        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        var beforeRaw=GC.GetAllocatedBytesForCurrentThread();
        var (rawOk,rawCount)=RawTryGetNonEnumeratedCountImpl();
        var rawBytes=GC.GetAllocatedBytesForCurrentThread()-beforeRaw;

        Assert.Equal(rawOk,flinqOk);
        Assert.Equal(rawCount,flinqCount);
        Assert.True(flinqBytes<=rawBytes,
            $"FLinq allocated {flinqBytes}B, raw System.Linq allocated {rawBytes}B.");
    }

    private static (bool,int) FLinqTryGetNonEnumeratedCountImpl() {
        var ok=global::System.Linq.Enumerable.TryGetNonEnumeratedCount(Source(),out var count);
        return (ok,count);
    }

    [LinqIntercept(false)]
    private static (bool,int) RawTryGetNonEnumeratedCountImpl() {
        var ok=global::System.Linq.Enumerable.TryGetNonEnumeratedCount(Source(),out var count);
        return (ok,count);
    }

    private static global::Seamless.Collections.Tsavorite.HashSet<int> Source()=>source??throw new InvalidOperationException("Source was not initialized.");
}
