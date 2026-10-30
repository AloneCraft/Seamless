using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class StatementInterceptionBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, int ElementCount, bool HasCapture, long AllocatedBytes, double ElapsedUs, int OutputCount);

    // ==========================================
    // 1. 各方式の実装
    // ==========================================

    // ① 従来の System.Linq (FLinq による自動インターセプトを確実に無効化して計測)
    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunSystemLinq_NonCapturing(int[] array)
    {
        return array.Where(x => x > 10).Select(x => x * 2).Count();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunSystemLinq_Capturing(int[] array, int threshold)
    {
        return array.Where(x => x > threshold).Select(x => x * 2).Count();
    }

    // ② 手書き Ideal ループ (限界性能)
    private static int RunIdeal_NonCapturing(int[] array)
    {
        int count = 0;
        for (int i = 0; i < array.Length; i++)
        {
            int x = array[i];
            if (x > 10)
            {
                count++;
            }
        }
        return count;
    }

    private static int RunIdeal_Capturing(int[] array, int threshold)
    {
        int count = 0;
        for (int i = 0; i < array.Length; i++)
        {
            int x = array[i];
            if (x > threshold)
            {
                count++;
            }
        }
        return count;
    }

    // ③ Seamless FLinq 自動インターセプト (属性も partial もない素の C# コード)
    private static int RunFLinq_NonCapturing(int[] array)
    {
        return array.Where(x => x > 10).Select(x => x * 2).Count();
    }

    private static int RunFLinq_Capturing(int[] array, int threshold)
    {
        return array.Where(x => x > threshold).Select(x => x * 2).Count();
    }

    // ③ 新仕様 Pass-Through 方式 (手前素通し + 終端融合インライン)
    // 中間操作はインスタンスを作らず元配列を素通し
    private static IEnumerable<T> PassThrough_Where<T>(IEnumerable<T> source, Func<T, bool> predicate) => source;
    private static IEnumerable<TResult> PassThrough_Select<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector) => (IEnumerable<TResult>)source;

    // 終端操作で元配列を受け取り、融合ループを実行
    private static int PassThrough_Count_NonCapturing(IEnumerable<int> source)
    {
        int[] raw = (int[])source;
        int count = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            int x = raw[i];
            if (x > 10) count++;
        }
        return count;
    }

    private static int PassThrough_Count_Capturing(IEnumerable<int> source, int threshold)
    {
        int[] raw = (int[])source;
        int count = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            int x = raw[i];
            if (x > threshold) count++;
        }
        return count;
    }

    private static int RunPassThrough_NonCapturing(int[] array)
    {
        // Interceptor が入った場合のコード実行フローをシミュレート
        var s1 = PassThrough_Where(array, x => x > 10);
        var s2 = PassThrough_Select(s1, x => x * 2);
        return PassThrough_Count_NonCapturing(s2);
    }

    private static int RunPassThrough_Capturing(int[] array, int threshold)
    {
        // Interceptor が入った場合のコード実行フローをシミュレート
        var s1 = PassThrough_Where(array, x => x > threshold);
        var s2 = PassThrough_Select(s1, x => x * 2);
        return PassThrough_Count_Capturing(s2, threshold);
    }

    // ==========================================
    // 測定ユーティリティ
    // ==========================================
    private static (long AllocatedBytes, double ElapsedUs, int Result) Measure(Func<int> action, int warmup = 5, int iterations = 100)
    {
        // ウォームアップ
        for (int i = 0; i < warmup; i++)
        {
            action();
        }

        // GC 安定化
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // メモリ確保量計測 (1回実行)
        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        int result = action();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;

        // 実行速度計測 (複数回反復の平均時間)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }
        sw.Stop();
        double elapsedUs = (sw.Elapsed.TotalMicroseconds) / iterations;

        return (allocated, elapsedUs, result);
    }

    [Fact]
    public void Benchmark_Compare_SystemLinq_vs_Ideal_vs_PassThrough()
    {
        var testSizes = new[] { 1_000, 100_000 };
        var results = new List<BenchmarkResult>();

        foreach (var size in testSizes)
        {
            var array = Enumerable.Range(1, size).ToArray();
            int threshold = 10;

            // ----------------------------------------------------
            // A. キャプチャなし (p => true / x => x > 10)
            // ----------------------------------------------------
            var (allocSys1, timeSys1, resSys1) = Measure(() => RunSystemLinq_NonCapturing(array));
            var (allocIdeal1, timeIdeal1, resIdeal1) = Measure(() => RunIdeal_NonCapturing(array));
            var (allocPass1, timePass1, resPass1) = Measure(() => RunPassThrough_NonCapturing(array));
            var (allocFLinq1, timeFLinq1, resFLinq1) = Measure(() => RunFLinq_NonCapturing(array));

            results.Add(new("1. System.Linq", size, false, allocSys1, timeSys1, resSys1));
            results.Add(new("2. Ideal Loop", size, false, allocIdeal1, timeIdeal1, resIdeal1));
            results.Add(new("3. Pass-Through (手前素通し)", size, false, allocPass1, timePass1, resPass1));
            results.Add(new("4. FLinq 自動インターセプト", size, false, allocFLinq1, timeFLinq1, resFLinq1));

            // 計算結果の整合性検証
            Assert.Equal(resSys1, resIdeal1);
            Assert.Equal(resSys1, resPass1);
            Assert.Equal(resSys1, resFLinq1);

            // アロケーション検証: Ideal, Pass-Through, FLinq は完全 0 bytes
            Assert.Equal(0, allocIdeal1);
            Assert.Equal(0, allocPass1);
            Assert.Equal(0, allocFLinq1);

            // ----------------------------------------------------
            // B. キャプチャあり (x => x > threshold)
            // ----------------------------------------------------
            var (allocSys2, timeSys2, resSys2) = Measure(() => RunSystemLinq_Capturing(array, threshold));
            var (allocIdeal2, timeIdeal2, resIdeal2) = Measure(() => RunIdeal_Capturing(array, threshold));
            var (allocPass2, timePass2, resPass2) = Measure(() => RunPassThrough_Capturing(array, threshold));
            var (allocFLinq2, timeFLinq2, resFLinq2) = Measure(() => RunFLinq_Capturing(array, threshold));

            results.Add(new("1. System.Linq (Capture)", size, true, allocSys2, timeSys2, resSys2));
            results.Add(new("2. Ideal Loop (Capture)", size, true, allocIdeal2, timeIdeal2, resIdeal2));
            results.Add(new("3. Pass-Through (Capture)", size, true, allocPass2, timePass2, resPass2));
            results.Add(new("4. FLinq 自動インターセプト (Capture)", size, true, allocFLinq2, timeFLinq2, resFLinq2));

            // 計算結果の整合性検証
            Assert.Equal(resSys2, resIdeal2);
            Assert.Equal(resSys2, resPass2);
            Assert.Equal(resSys2, resFLinq2);

            // アロケーション検証:
            // 通常 LINQ ではクロージャアロケーション (>0 bytes) が発生する
            Assert.True(allocSys2 > 0, $"System.Linq with capture should allocate heap memory, but was {allocSys2}");
            // 手前素通しは引数評価時にクロージャ(88 bytes)が発生する
            Assert.True(allocPass2 > 0, $"Pass-Through has closure allocation on caller argument evaluation: {allocPass2}");
            // 一方、FLinq 自動インターセプトはクロージャごと消滅させて完全 0 bytes！
            Assert.Equal(0, allocIdeal2);
            Assert.Equal(0, allocFLinq2);
        }

        // ==========================================
        // ベンチマーク結果テーブルの出力
        // ==========================================
        output.WriteLine("==========================================================================================");
        output.WriteLine($"| {"方式",-32} | {"要素数",8} | {"キャプチャ",8} | {"Allocated",12} | {"時間 (μs)",10} |");
        output.WriteLine("==========================================================================================");
        foreach (var r in results)
        {
            string captureStr = r.HasCapture ? "あり" : "なし";
            output.WriteLine($"| {r.Method,-30} | {r.ElementCount,10:N0} | {captureStr,10} | {r.AllocatedBytes,10:N0} B | {r.ElapsedUs,10:F2} μs |");
        }
        output.WriteLine("==========================================================================================");
    }
}
