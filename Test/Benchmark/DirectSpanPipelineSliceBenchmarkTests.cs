using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class DirectSpanPipelineSliceBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedUs, long OutputSum);

    // ==========================================
    // 1. 先頭スライス (Leading Slice: Skip -> Take -> Where -> Sum)
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunLeadingSlice_SystemLinq(int[] array, int skip, int take, int threshold)
    {
        return array.Skip(skip).Take(take).Where(x => x > threshold).Sum();
    }

    private static int RunLeadingSlice_Ideal(int[] array, int skip, int take, int threshold)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int sum = 0;
        for (int i = startIndex; i < endIndex; i++)
        {
            int val = span[i];
            if (val > threshold)
            {
                checked { sum += val; }
            }
        }
        return sum;
    }

    private static int RunLeadingSlice_FLinq(int[] array, int skip, int take, int threshold)
    {
        return array.Skip(skip).Take(take).Where(x => x > threshold).Sum();
    }

    // ==========================================
    // 2. フィルタ後スライス (Filtered Slice: Where -> Skip -> Take -> Sum)
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunFilteredSlice_SystemLinq(int[] array, int threshold, int skip, int take)
    {
        return array.Where(x => x > threshold).Skip(skip).Take(take).Sum();
    }

    private static int RunFilteredSlice_Ideal(int[] array, int threshold, int skip, int take)
    {
        ReadOnlySpan<int> span = array;
        int sum = 0;
        int skipped = 0;
        int taken = 0;
        for (int i = 0; i < span.Length; i++)
        {
            int val = span[i];
            if (val > threshold)
            {
                if (skipped < skip)
                {
                    skipped++;
                    continue;
                }
                if (taken >= take)
                {
                    break;
                }
                taken++;
                checked { sum += val; }
            }
        }
        return sum;
    }

    private static int RunFilteredSlice_FLinq(int[] array, int threshold, int skip, int take)
    {
        return array.Where(x => x > threshold).Skip(skip).Take(take).Sum();
    }

    // ==========================================
    // 3. 末尾スライス (Trailing Slice: SkipLast -> TakeLast -> Where -> Sum)
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunTrailingSlice_SystemLinq(int[] array, int skipLast, int takeLast, int threshold)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).Where(x => x > threshold).Sum();
    }

    private static int RunTrailingSlice_Ideal(int[] array, int skipLast, int takeLast, int threshold)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = 0;
        int endIndex = span.Length;
        endIndex = Math.Max(startIndex, endIndex - Math.Max(0, skipLast));
        startIndex = Math.Max(startIndex, endIndex - Math.Max(0, takeLast));
        int sum = 0;
        for (int i = startIndex; i < endIndex; i++)
        {
            int val = span[i];
            if (val > threshold)
            {
                checked { sum += val; }
            }
        }
        return sum;
    }

    private static int RunTrailingSlice_FLinq(int[] array, int skipLast, int takeLast, int threshold)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).Where(x => x > threshold).Sum();
    }

    // ==========================================
    // 測定ユーティリティ
    // ==========================================
    private static (long AllocatedBytes, double ElapsedUs, int Result) Measure(Func<int> action, int warmup = 5, int iterations = 100)
    {
        for (int i = 0; i < warmup; i++)
        {
            action();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        int result = action();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }
        sw.Stop();
        double elapsedUs = sw.Elapsed.TotalMicroseconds / iterations;

        return (allocated, elapsedUs, result);
    }

    [Fact]
    public void Benchmark_Slice_Fusion_Leading_Filtered_And_Trailing()
    {
        const int size = 100_000;
        var array = Enumerable.Range(1, size).ToArray();
        const int skip = 5_000;
        const int take = 20_000;
        const int threshold = 10_000;

        var results = new List<BenchmarkResult>();

        // 1. 先頭スライス (Leading Slice)
        var (allocLeadSys, timeLeadSys, resLeadSys) = Measure(() => RunLeadingSlice_SystemLinq(array, skip, take, threshold));
        var (allocLeadIdeal, timeLeadIdeal, resLeadIdeal) = Measure(() => RunLeadingSlice_Ideal(array, skip, take, threshold));
        var (allocLeadFLinq, timeLeadFLinq, resLeadFLinq) = Measure(() => RunLeadingSlice_FLinq(array, skip, take, threshold));

        results.Add(new("1. System.Linq", "Leading (Skip->Take->Where->Sum)", size, allocLeadSys, timeLeadSys, resLeadSys));
        results.Add(new("2. Ideal Loop", "Leading (Skip->Take->Where->Sum)", size, allocLeadIdeal, timeLeadIdeal, resLeadIdeal));
        results.Add(new("3. FLinq Direct Fusion", "Leading (Skip->Take->Where->Sum)", size, allocLeadFLinq, timeLeadFLinq, resLeadFLinq));

        Assert.Equal(resLeadSys, resLeadIdeal);
        Assert.Equal(resLeadSys, resLeadFLinq);
        Assert.Equal(0, allocLeadIdeal);
        Assert.Equal(0, allocLeadFLinq);

        // 2. フィルタ後スライス (Filtered Slice)
        var (allocFiltSys, timeFiltSys, resFiltSys) = Measure(() => RunFilteredSlice_SystemLinq(array, threshold, skip, take));
        var (allocFiltIdeal, timeFiltIdeal, resFiltIdeal) = Measure(() => RunFilteredSlice_Ideal(array, threshold, skip, take));
        var (allocFiltFLinq, timeFiltFLinq, resFiltFLinq) = Measure(() => RunFilteredSlice_FLinq(array, threshold, skip, take));

        results.Add(new("1. System.Linq", "Filtered (Where->Skip->Take->Sum)", size, allocFiltSys, timeFiltSys, resFiltSys));
        results.Add(new("2. Ideal Loop", "Filtered (Where->Skip->Take->Sum)", size, allocFiltIdeal, timeFiltIdeal, resFiltIdeal));
        results.Add(new("3. FLinq Direct Fusion", "Filtered (Where->Skip->Take->Sum)", size, allocFiltFLinq, timeFiltFLinq, resFiltFLinq));

        Assert.Equal(resFiltSys, resFiltIdeal);
        Assert.Equal(resFiltSys, resFiltFLinq);
        Assert.Equal(0, allocFiltIdeal);
        Assert.Equal(0, allocFiltFLinq);

        // 3. 末尾スライス (Trailing Slice)
        const int skipLast = 5_000;
        const int takeLast = 20_000;
        var (allocTrailSys, timeTrailSys, resTrailSys) = Measure(() => RunTrailingSlice_SystemLinq(array, skipLast, takeLast, threshold));
        var (allocTrailIdeal, timeTrailIdeal, resTrailIdeal) = Measure(() => RunTrailingSlice_Ideal(array, skipLast, takeLast, threshold));
        var (allocTrailFLinq, timeTrailFLinq, resTrailFLinq) = Measure(() => RunTrailingSlice_FLinq(array, skipLast, takeLast, threshold));

        results.Add(new("1. System.Linq", "Trailing (SkipLast->TakeLast->Where->Sum)", size, allocTrailSys, timeTrailSys, resTrailSys));
        results.Add(new("2. Ideal Loop", "Trailing (SkipLast->TakeLast->Where->Sum)", size, allocTrailIdeal, timeTrailIdeal, resTrailIdeal));
        results.Add(new("3. FLinq Direct Fusion", "Trailing (SkipLast->TakeLast->Where->Sum)", size, allocTrailFLinq, timeTrailFLinq, resTrailFLinq));

        Assert.Equal(resTrailSys, resTrailIdeal);
        Assert.Equal(resTrailSys, resTrailFLinq);
        Assert.Equal(0, allocTrailIdeal);
        Assert.Equal(0, allocTrailFLinq);

        // 出力テーブル
        output.WriteLine("| パターン | 方式 | 要素数 | メモリ (Allocated) | 実行時間 (平均) | 計算結果 |");
        output.WriteLine("| :--- | :--- | :--- | :--- | :--- | :--- |");
        foreach (var r in results)
        {
            output.WriteLine($"| {r.Pattern} | {r.Method} | {r.ElementCount:N0} | {r.AllocatedBytes:N0} B | {r.ElapsedUs:F2} μs | {r.OutputSum:N0} |");
        }
    }
}
