namespace Test.System.Linq.Intercept.Optimization;

// ToList の遅経路（件数も span も前もって出せないソース）が List<T>.Add の倍々成長ではなく
// GeneratedCodeHelpers.ToListFromValueEnumerator（スタックの InlineArray16 → ArrayPool の
// セグメント連結 → 総数確定後に正確サイズの List を1本だけ確保）を通ることを固定する。
//
// 計測は必ず「直接呼び出しの前後を GC カウンタで挟む」方式で書くこと。対象を Func<T>/delegate で
// ラップして共通ヘルパー経由で呼ぶと割当が計測から消える（再現性を確認済みの現象）。
public sealed class ToListBuffering:OptimizationTestBase {
    internal const int ElementCount = 4096;

    // 件数も span も前もって出せないソース。FLinq 側は Root の汎用 struct 経由になり、
    // TryGetNonEnumeratedCount / TryGetSpan がいずれも false を返して遅経路へ落ちる。
    public static IEnumerable<int> Uncountable(int count) {
        for(var i = 0;i<count;i++) {
            yield return i;
        }
    }

    [Fact, Trait("Category","Unit")]
    public static void ToList() {
        {
            // 単独 ToList の遅経路。倍々成長（naive）ではなく正確サイズ1本（ideal）に一致すること。
            var ideal = MeasureIdealToList();
            var naive = MeasureNaiveToList();
            var flinq = MeasureFLinqToList();
            Assert.True(naive>=ideal*3/2,$"baseline sanity failed. naive={naive} ideal={ideal}");
            Assert.True(flinq<=ideal+1024,$"FLinq allocated too much. flinq={flinq} ideal={ideal} naive={naive}");
        }
        {
            // 融合パイプライン（Where を挟む）の ToList 遅経路も同じ経路に乗ること。
            var ideal = MeasureIdealToList();
            var naive = MeasureNaiveToList();
            var flinq = MeasureFLinqWhereToList();
            Assert.True(naive>=ideal*3/2,$"baseline sanity failed. naive={naive} ideal={ideal}");
            Assert.True(flinq<=ideal+1024,$"FLinq allocated too much. flinq={flinq} ideal={ideal} naive={naive}");
        }
        {
            // 空列・InlineArray16 の境界（16/17）・大きい件数で内容と順序が変わらないこと。
            foreach(var count in new[] { 0,1,16,17,4096 }) {
                var expected = new List<int>();
                foreach(var value in Uncountable(count)) {
                    expected.Add(value);
                }
                ToListContentOptimized(count,expected);
                ToListContentNoOptimized(count,expected);
            }
        }
    }

    // 理想値: 総数が分かっている前提で正確サイズの List を1本だけ確保して詰めた場合。
    private static long MeasureIdealToList() {
        FillPresized();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = FillPresized();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(ElementCount,result.Count);
        return allocated;
    }

    // 修正前の生成コードと同じ形。new List<T>() の倍々成長で合計およそ 2N を確保する。
    private static long MeasureNaiveToList() {
        FillGrowing();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = FillGrowing();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(ElementCount,result.Count);
        return allocated;
    }

    // 傍受メソッドの本文は生成コードへコピーされ、囲むクラスのスコープが失われる。
    // ElementCount / Uncountable は型名で修飾しないと生成側で CS0103 になる。
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static long MeasureFLinqToList() {
        _=ToListBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.ToListBuffering.ElementCount).ToList();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = ToListBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.ToListBuffering.ElementCount).ToList();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(global::Test.System.Linq.Intercept.Optimization.ToListBuffering.ElementCount,result.Count);
        return allocated;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static long MeasureFLinqWhereToList() {
        _=ToListBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.ToListBuffering.ElementCount).Where(static value => value>=0).ToList();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = ToListBuffering.Uncountable(global::Test.System.Linq.Intercept.Optimization.ToListBuffering.ElementCount).Where(static value => value>=0).ToList();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(global::Test.System.Linq.Intercept.Optimization.ToListBuffering.ElementCount,result.Count);
        return allocated;
    }

    private static List<int> FillPresized() {
        var list = new List<int>(ElementCount);
        foreach(var value in Uncountable(ElementCount)) {
            list.Add(value);
        }
        return list;
    }

    private static List<int> FillGrowing() {
        var list = new List<int>();
        foreach(var value in Uncountable(ElementCount)) {
            list.Add(value);
        }
        return list;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ToListContentOptimized(int count,dynamic expected) {
        AssertSequenceEqual(expected,ToListBuffering.Uncountable(count).ToList());
        AssertSequenceEqual(expected,ToListBuffering.Uncountable(count).Where(static value => value>=0).ToList());
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static void ToListContentNoOptimized(int count,dynamic expected) {
        AssertSequenceEqual(expected,ToListBuffering.Uncountable(count).ToList());
        AssertSequenceEqual(expected,ToListBuffering.Uncountable(count).Where(static value => value>=0).ToList());
    }
}
