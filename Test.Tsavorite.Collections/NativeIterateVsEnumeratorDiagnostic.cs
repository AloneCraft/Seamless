using System.Diagnostics;

namespace Test.Tsavorite.Collections;

// クラスタA検証用の使い捨て診断: HashSet<int>.Iterate<TStrategy>(ネイティブ Iterate 経路)が
// 通常の foreach(GetEnumerator 経路)に対してどれだけ遅いかを、FLinq/生成コードを介さず直接測る。
// 目的が済んだら削除してよい。
public sealed class NativeIterateVsEnumeratorDiagnostic(Xunit.Abstractions.ITestOutputHelper output) {
    private struct CountWherePredicate:IHashSetIterateStrategy<CountWherePredicate,int> {
        public int Count;
        public static bool Invoke(ref CountWherePredicate self,int item) {
            if(item>2048) self.Count++;
            return true;
        }
    }

    [Fact]
    public void Iterate対foreachの生時間を比較() {
        const int elementCount = 4_096;
        var store = Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: Seamless.Collections.Tsavorite.Store.GetIndexSizeForCapacity(elementCount),
            MemorySize: 1L<<24)
            .Create<int>(new MemoryPackSerializerStrategy());
        using var set = store.Create();
        var random = new Random(12345);
        var values = Enumerable.Range(1,elementCount).OrderBy(_ => random.Next()).ToArray();
        foreach(var v in values) set.Add(v);

        const int iterations = 200;

        // ウォームアップ
        for(var i = 0;i<10;i++) {
            var strategy = new CountWherePredicate();
            set.Iterate(ref strategy);
            var plain = 0;
            foreach(var item in set) if(item>2048) plain++;
        }

        var swIterate = Stopwatch.StartNew();
        var totalIterate = 0;
        for(var i = 0;i<iterations;i++) {
            var strategy = new CountWherePredicate();
            set.Iterate(ref strategy);
            totalIterate+=strategy.Count;
        }
        swIterate.Stop();

        var swForeach = Stopwatch.StartNew();
        var totalForeach = 0;
        for(var i = 0;i<iterations;i++) {
            var count = 0;
            foreach(var item in set) if(item>2048) count++;
            totalForeach+=count;
        }
        swForeach.Stop();

        var iterateNsPerOp = swIterate.Elapsed.TotalNanoseconds/iterations;
        var foreachNsPerOp = swForeach.Elapsed.TotalNanoseconds/iterations;
        output.WriteLine($"Iterate<TStrategy>: {iterateNsPerOp:F0} ns/op (checksum={totalIterate})");
        output.WriteLine($"foreach+GetEnumerator: {foreachNsPerOp:F0} ns/op (checksum={totalForeach})");
        output.WriteLine($"ratio (Iterate/foreach): {iterateNsPerOp/foreachNsPerOp:F3}x");
        Assert.Equal(totalIterate,totalForeach);
    }
}
