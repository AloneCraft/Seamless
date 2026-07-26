namespace Test.System.Linq.Intercept.Optimization;

// TakeLast の「末尾 N 件のバッファ」が GC ヒープの Queue<T> ではなく
// ArrayPool ベースの ValueQueue<T> を使うことを固定する。
// Queue<T> は要素数分の配列を毎回 GC ヒープへ確保していた（int・N=1024 で約 4,100B）。
//
// 経路が2つあり、駆動方法で分かれるので両方を測る:
//   - 単独フラグメント（TakeLast.cs）: `.TakeLast(n).Consume(...)` のように後段が
//     LINQ 終端でない形。実測で負けていた Benchmark の TakeLast_count もこの形。
//   - 融合パイプライン段: `.TakeLast(n).Count()` のように LINQ 終端が続く形。
//     こちらは A3 で既に ArrayPool 化済みだが、退行させないため一緒に固定する。
//
// 計測は必ず「直接呼び出しの前後を GC カウンタで挟む」方式で書くこと。対象を Func<T>/delegate で
// ラップして共通ヘルパー経由で呼ぶと割当が計測から消える（再現性を確認済みの現象）。
public sealed class TakeLastBuffering:OptimizationTestBase {
    internal const int ElementCount = 4096;
    internal const int TakeCount = 1024;
    private const int AttemptCount = 8;

    // Items を溜めない Consumer。割当計測を汚さないよう static で持つ。
    internal static readonly CountingConsumer Sink = new();

    // 件数も span も前もって出せないソース。TakeLast はバッファ経路へ落ちる。
    public static IEnumerable<int> Uncountable(int count) {
        for(var i = 0;i<count;i++) {
            yield return i;
        }
    }

    [Fact, Trait("Category","Unit")]
    public static void TakeLast() {
        {
            // 単独フラグメント経路。ウォームアップ後はプールから借りるだけなので割当がほぼ消えること。
            // ArrayPool.Shared は他のテストと共有で、並列実行中に暖めた配列を横取りされることがある。
            // そのため数回試して最小値を採る。修正が無ければ毎回 GC ヒープへ確保するので最小値も跳ね上がり、
            // ガードの検出力は落ちない。計測本体を Func<T> でラップしないこと（割当が計測から消える）。
            var naive = MeasureNaiveTakeLast();
            var flinq = long.MaxValue;
            for(var attempt = 0;attempt<AttemptCount;attempt++) {
                WarmUpFLinqTakeLastConsume();
                var allocated = MeasureFLinqTakeLastConsume();
                if(allocated<flinq) {
                    flinq=allocated;
                }
            }
            Assert.True(naive>=4000,$"baseline sanity failed. naive={naive}");
            Assert.True(flinq<=512,$"FLinq allocated too much. flinq={flinq} naive={naive}");
        }
        {
            // 融合パイプライン段。A3 で ArrayPool 化済みなので、退行していないことの確認。
            var naive = MeasureNaiveTakeLast();
            var flinq = long.MaxValue;
            for(var attempt = 0;attempt<AttemptCount;attempt++) {
                _=WarmUpFLinqTakeLastCount();
                var allocated = MeasureFLinqTakeLastCount();
                if(allocated<flinq) {
                    flinq=allocated;
                }
            }
            Assert.True(naive>=4000,$"baseline sanity failed. naive={naive}");
            Assert.True(flinq<=512,$"FLinq allocated too much. flinq={flinq} naive={naive}");
        }
        {
            // 取る件数が 0 / 1 / 要素数ちょうど / 要素数超過。
            foreach(var count in new[] { 0,1,16,17,4096,5000 }) {
                var window = new Queue<int>();
                foreach(var value in Uncountable(4096)) {
                    window.Enqueue(value);
                    if(window.Count>count) {
                        window.Dequeue();
                    }
                }
                var expected = new List<int>(window);
                TakeLastContentOptimized(count,expected);
                TakeLastContentNoOptimized(count,expected);
            }
        }
        {
            // 多段 TakeLast は EffectiveTakeCount が Math.Min で畳まれる。
            var window = new Queue<int>();
            foreach(var value in Uncountable(4096)) {
                window.Enqueue(value);
                if(window.Count>16) {
                    window.Dequeue();
                }
            }
            var expected = new List<int>(window);
            TakeLastChainedOptimized(expected);
            TakeLastChainedNoOptimized(expected);
        }
    }

    // 修正前の生成コードと同じ形。Queue<T> が要素数分の配列を GC ヒープへ確保する。
    private static long MeasureNaiveTakeLast() {
        NaiveTakeLastCount();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = NaiveTakeLastCount();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(TakeCount,result);
        return allocated;
    }

    // ウォームアップは必ず別メソッドに置くこと。生成コードは enumerator を
    // `using var` で受けるためスコープがメソッド末尾までになり、同じメソッド内で
    // 暖機すると借りた配列が計測開始前に ArrayPool へ返らず、計測対象が
    // 空のプールから新規確保してしまう（プールの効果が見えなくなる）。
    //
    // 傍受メソッドの本文は生成コードへコピーされ、囲むクラスのスコープが失われる。
    // 定数と静的フィールドは global:: 付きで完全修飾しないと生成側で解決できない。
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void WarmUpFLinqTakeLastConsume() {
        TakeLastBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.ElementCount).TakeLast(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.TakeCount).Consume(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.Sink);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static long MeasureFLinqTakeLastConsume() {
        var before = GC.GetAllocatedBytesForCurrentThread();
        TakeLastBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.ElementCount).TakeLast(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.TakeCount).Consume(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.Sink);
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        return allocated;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int WarmUpFLinqTakeLastCount() =>
        TakeLastBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.ElementCount).TakeLast(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.TakeCount).Count();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static long MeasureFLinqTakeLastCount() {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = TakeLastBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.ElementCount).TakeLast(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.TakeCount).Count();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(global::Test.System.Linq.Intercept.Optimization.TakeLastBuffering.TakeCount,result);
        return allocated;
    }

    private static int NaiveTakeLastCount() {
        var window = new Queue<int>(TakeCount);
        foreach(var value in Uncountable(ElementCount)) {
            if(window.Count==TakeCount) {
                window.Dequeue();
            }
            window.Enqueue(value);
        }
        return window.Count;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void TakeLastContentOptimized(int count,dynamic expected) {
        AssertSequenceEqual(expected,TakeLastBuffering.Uncountable(4096).TakeLast(count).ToList());
        AssertSequenceEqual(expected,TakeLastBuffering.Uncountable(4096).Where(static value => value>=0).TakeLast(count).ToList());
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static void TakeLastContentNoOptimized(int count,dynamic expected) {
        AssertSequenceEqual(expected,TakeLastBuffering.Uncountable(4096).TakeLast(count).ToList());
        AssertSequenceEqual(expected,TakeLastBuffering.Uncountable(4096).Where(static value => value>=0).TakeLast(count).ToList());
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void TakeLastChainedOptimized(dynamic expected) {
        AssertSequenceEqual(expected,TakeLastBuffering.Uncountable(4096).TakeLast(64).TakeLast(16).ToList());
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static void TakeLastChainedNoOptimized(dynamic expected) {
        AssertSequenceEqual(expected,TakeLastBuffering.Uncountable(4096).TakeLast(64).TakeLast(16).ToList());
    }
}
