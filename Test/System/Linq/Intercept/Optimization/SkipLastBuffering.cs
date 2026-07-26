namespace Test.System.Linq.Intercept.Optimization;

// SkipLast の「直近 N 件のスライディングウィンドウ」が GC ヒープの Queue<T> ではなく
// ArrayPool ベースの ValueQueue<T> を使うことを固定する。
// Queue<T> は要素数+1 の配列を毎回 GC ヒープへ確保していた（int・N=1024 で約 4,100B）。
//
// 計測は必ず「直接呼び出しの前後を GC カウンタで挟む」方式で書くこと。対象を Func<T>/delegate で
// ラップして共通ヘルパー経由で呼ぶと割当が計測から消える（再現性を確認済みの現象）。
public sealed class SkipLastBuffering:OptimizationTestBase {
    internal const int ElementCount = 4096;
    internal const int SkipCount = 1024;

    // 件数も span も前もって出せないソース。SkipLast はスライディングウィンドウ経路へ落ちる。
    public static IEnumerable<int> Uncountable(int count) {
        for(var i = 0;i<count;i++) {
            yield return i;
        }
    }

    [Fact, Trait("Category","Unit")]
    public static void SkipLast() {
        {
            // 単独 SkipLast。ウォームアップ後はプールから借りるだけなので割当がほぼ消えること。
            var naive = MeasureNaiveSkipLast();
            _=WarmUpFLinqSkipLast();
            var flinq = MeasureFLinqSkipLast();
            Assert.True(naive>=4000,$"baseline sanity failed. naive={naive}");
            Assert.True(flinq<=512,$"FLinq allocated too much. flinq={flinq} naive={naive}");
        }
        {
            // 融合パイプライン（Where を挟む）の SkipLast 段も同じ経路に乗ること。
            var naive = MeasureNaiveSkipLast();
            _=WarmUpFLinqWhereSkipLast();
            var flinq = MeasureFLinqWhereSkipLast();
            Assert.True(naive>=4000,$"baseline sanity failed. naive={naive}");
            Assert.True(flinq<=512,$"FLinq allocated too much. flinq={flinq} naive={naive}");
        }
        {
            // 捨てる件数が 0 / 1 / 要素数ちょうど / 要素数超過、および多段（EffectiveSkipCount の合算）。
            foreach(var count in new[] { 0,1,16,17,4096,5000 }) {
                var expected = new List<int>();
                var window = new Queue<int>();
                foreach(var value in Uncountable(4096)) {
                    window.Enqueue(value);
                    if(window.Count>count) {
                        expected.Add(window.Dequeue());
                    }
                }
                SkipLastContentOptimized(count,expected);
                SkipLastContentNoOptimized(count,expected);
            }
        }
    }

    // 修正前の生成コードと同じ形。Queue<T> が要素数+1 の配列を GC ヒープへ確保する。
    private static long MeasureNaiveSkipLast() {
        NaiveSkipLastCount();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = NaiveSkipLastCount();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(ElementCount-SkipCount,result);
        return allocated;
    }

    // ウォームアップは必ず別メソッドに置くこと。生成コードは enumerator を
    // `using var` で受けるためスコープがメソッド末尾までになり、同じメソッド内で
    // 暖機すると借りた配列が計測開始前に ArrayPool へ返らず、計測対象が
    // 空のプールから新規確保してしまう（プールの効果が見えなくなる）。
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int WarmUpFLinqSkipLast() =>
        SkipLastBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.ElementCount).SkipLast(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.SkipCount).Count();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int WarmUpFLinqWhereSkipLast() =>
        SkipLastBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.ElementCount).Where(static value => value>=0).SkipLast(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.SkipCount).Count();

    // 傍受メソッドの本文は生成コードへコピーされ、囲むクラスのスコープが失われる。
    // 定数は global:: 付きで完全修飾しないと生成側で解決できない。
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static long MeasureFLinqSkipLast() {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = SkipLastBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.ElementCount).SkipLast(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.SkipCount).Count();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.ElementCount-global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.SkipCount,result);
        return allocated;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static long MeasureFLinqWhereSkipLast() {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = SkipLastBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.ElementCount).Where(static value => value>=0).SkipLast(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.SkipCount).Count();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.ElementCount-global::Test.System.Linq.Intercept.Optimization.SkipLastBuffering.SkipCount,result);
        return allocated;
    }

    private static int NaiveSkipLastCount() {
        var window = new Queue<int>(SkipCount+1);
        var count = 0;
        foreach(var value in Uncountable(ElementCount)) {
            window.Enqueue(value);
            if(window.Count>SkipCount) {
                window.Dequeue();
                count++;
            }
        }
        return count;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void SkipLastContentOptimized(int count,dynamic expected) {
        AssertSequenceEqual(expected,SkipLastBuffering.Uncountable(4096).SkipLast(count).ToList());
        AssertSequenceEqual(expected,SkipLastBuffering.Uncountable(4096).Where(static value => value>=0).SkipLast(count).ToList());
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static void SkipLastContentNoOptimized(int count,dynamic expected) {
        AssertSequenceEqual(expected,SkipLastBuffering.Uncountable(4096).SkipLast(count).ToList());
        AssertSequenceEqual(expected,SkipLastBuffering.Uncountable(4096).Where(static value => value>=0).SkipLast(count).ToList());
    }
}
