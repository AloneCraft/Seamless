using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class StatementInterceptionBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, int ElementCount, bool HasCapture, long AllocatedBytes, double ElapsedUs, int OutputCount);

    // ==========================================
    // 1. 各方式の実装 (5 方式の比較)
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

    // ② Naive Loop (一般的な手書き for ループ)
    private static int RunNaive_NonCapturing(int[] array)
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

    private static int RunNaive_Capturing(int[] array, int threshold)
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

    // ③ True Ideal Loop (境界チェック完全排除・MemoryMarshal 直接走査による真の手書き限界)
    private static int RunTrueIdeal_NonCapturing(int[] array)
    {
        ref int ptr = ref MemoryMarshal.GetArrayDataReference(array);
        ref int end = ref Unsafe.Add(ref ptr, array.Length);
        int count = 0;
        while (Unsafe.IsAddressLessThan(ref ptr, ref end))
        {
            var item = ptr;
            ptr = ref Unsafe.Add(ref ptr, 1);
            if (item > 10) count++;
        }
        return count;
    }

    private static int RunTrueIdeal_Capturing(int[] array, int threshold)
    {
        ref int ptr = ref MemoryMarshal.GetArrayDataReference(array);
        ref int end = ref Unsafe.Add(ref ptr, array.Length);
        int count = 0;
        while (Unsafe.IsAddressLessThan(ref ptr, ref end))
        {
            var item = ptr;
            ptr = ref Unsafe.Add(ref ptr, 1);
            if (item > threshold) count++;
        }
        return count;
    }

    // ④ Pass-Through 方式 (手前素通し + 終端融合インライン)
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
        var s1 = PassThrough_Where(array, x => x > 10);
        var s2 = PassThrough_Select(s1, x => x * 2);
        return PassThrough_Count_NonCapturing(s2);
    }

    private static int RunPassThrough_Capturing(int[] array, int threshold)
    {
        var s1 = PassThrough_Where(array, x => x > threshold);
        var s2 = PassThrough_Select(s1, x => x * 2);
        return PassThrough_Count_Capturing(s2, threshold);
    }

    // ⑤ Seamless FLinq 自動インターセプト (属性も partial もない素の C# コードから真の限界へ自動展開)
    private static int RunFLinq_NonCapturing(int[] array)
    {
        return array.Where(x => x > 10).Select(x => x * 2).Count();
    }

    private static int RunFLinq_Capturing(int[] array, int threshold)
    {
        return array.Where(x => x > threshold).Select(x => x * 2).Count();
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
            var (allocNaive1, timeNaive1, resNaive1) = Measure(() => RunNaive_NonCapturing(array));
            var (allocTrue1, timeTrue1, resTrue1) = Measure(() => RunTrueIdeal_NonCapturing(array));
            var (allocPass1, timePass1, resPass1) = Measure(() => RunPassThrough_NonCapturing(array));
            var (allocFLinq1, timeFLinq1, resFLinq1) = Measure(() => RunFLinq_NonCapturing(array));

            results.Add(new("1. System.Linq", size, false, allocSys1, timeSys1, resSys1));
            results.Add(new("2. Naive Loop (一般手書き)", size, false, allocNaive1, timeNaive1, resNaive1));
            results.Add(new("3. True Ideal (真の手書き限界)", size, false, allocTrue1, timeTrue1, resTrue1));
            results.Add(new("4. Pass-Through (手前素通し)", size, false, allocPass1, timePass1, resPass1));
            results.Add(new("5. FLinq 自動インターセプト", size, false, allocFLinq1, timeFLinq1, resFLinq1));

            // 計算結果の整合性検証
            Assert.Equal(resSys1, resNaive1);
            Assert.Equal(resSys1, resTrue1);
            Assert.Equal(resSys1, resPass1);
            Assert.Equal(resSys1, resFLinq1);

            // アロケーション検証: 手書き・Pass-Through・FLinq は完全 0 bytes
            Assert.Equal(0, allocNaive1);
            Assert.Equal(0, allocTrue1);
            Assert.Equal(0, allocPass1);
            Assert.Equal(0, allocFLinq1);

            // ----------------------------------------------------
            // B. キャプチャあり (x => x > threshold)
            // ----------------------------------------------------
            var (allocSys2, timeSys2, resSys2) = Measure(() => RunSystemLinq_Capturing(array, threshold));
            var (allocNaive2, timeNaive2, resNaive2) = Measure(() => RunNaive_Capturing(array, threshold));
            var (allocTrue2, timeTrue2, resTrue2) = Measure(() => RunTrueIdeal_Capturing(array, threshold));
            var (allocPass2, timePass2, resPass2) = Measure(() => RunPassThrough_Capturing(array, threshold));
            var (allocFLinq2, timeFLinq2, resFLinq2) = Measure(() => RunFLinq_Capturing(array, threshold));

            results.Add(new("1. System.Linq (Capture)", size, true, allocSys2, timeSys2, resSys2));
            results.Add(new("2. Naive Loop (Capture)", size, true, allocNaive2, timeNaive2, resNaive2));
            results.Add(new("3. True Ideal (Capture)", size, true, allocTrue2, timeTrue2, resTrue2));
            results.Add(new("4. Pass-Through (Capture)", size, true, allocPass2, timePass2, resPass2));
            results.Add(new("5. FLinq 自動インターセプト (Capture)", size, true, allocFLinq2, timeFLinq2, resFLinq2));

            // 計算結果の整合性検証
            Assert.Equal(resSys2, resNaive2);
            Assert.Equal(resSys2, resTrue2);
            Assert.Equal(resSys2, resPass2);
            Assert.Equal(resSys2, resFLinq2);

            // アロケーション検証:
            // 通常 LINQ ではクロージャアロケーション (>0 bytes) が発生する
            Assert.True(allocSys2 > 0, $"System.Linq with capture should allocate heap memory, but was {allocSys2}");
            // 手前素通しは引数評価時にクロージャ(88 bytes)が発生する
            Assert.True(allocPass2 > 0, $"Pass-Through has closure allocation on caller argument evaluation: {allocPass2}");
            // 一方、FLinq 自動インターセプトおよび手書きはクロージャごと消滅させて完全 0 bytes！
            Assert.Equal(0, allocNaive2);
            Assert.Equal(0, allocTrue2);
            Assert.Equal(0, allocFLinq2);
        }

        // ==========================================
        // ベンチマーク結果テーブルの出力
        // ==========================================
        output.WriteLine("=================================================================================================");
        output.WriteLine($"| {"方式",-36} | {"要素数",8} | {"キャプチャ",8} | {"Allocated",12} | {"時間 (μs)",10} |");
        output.WriteLine("=================================================================================================");
        foreach (var r in results)
        {
            string captureStr = r.HasCapture ? "あり" : "なし";
            output.WriteLine($"| {r.Method,-34} | {r.ElementCount,10:N0} | {captureStr,10} | {r.AllocatedBytes,10:N0} B | {r.ElapsedUs,10:F2} μs |");
        }
        output.WriteLine("=================================================================================================");
    }
}
