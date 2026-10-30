using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class DirectSpanPipelineToDictionaryBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedUs, int OutputCount);

    // ==========================================
    // 1. Skip -> Take -> ToDictionary
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static Dictionary<int, int> RunSkipTakeToDict_SystemLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToDictionary(x => x, x => x * 2);
    }

    private static Dictionary<int, int> RunSkipTakeToDict_Ideal(int[] array, int skip, int take)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        var dict = new Dictionary<int, int>(length);
        for (int i = 0; i < length; i++)
        {
            int val = span[startIndex + i];
            dict.Add(val, val * 2);
        }
        return dict;
    }

    private static Dictionary<int, int> RunSkipTakeToDict_FLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToDictionary(x => x, x => x * 2);
    }

    // ==========================================
    // 2. SkipLast -> TakeLast -> ToDictionary
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static Dictionary<int, int> RunTrailingSliceToDict_SystemLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).ToDictionary(x => x, x => x * 2);
    }

    private static Dictionary<int, int> RunTrailingSliceToDict_Ideal(int[] array, int skipLast, int takeLast)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = 0;
        int endIndex = span.Length;
        endIndex = Math.Max(startIndex, endIndex - Math.Max(0, skipLast));
        startIndex = Math.Max(startIndex, endIndex - Math.Max(0, takeLast));
        int length = Math.Max(0, endIndex - startIndex);
        var dict = new Dictionary<int, int>(length);
        for (int i = 0; i < length; i++)
        {
            int val = span[startIndex + i];
            dict.Add(val, val * 2);
        }
        return dict;
    }

    private static Dictionary<int, int> RunTrailingSliceToDict_FLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).ToDictionary(x => x, x => x * 2);
    }

    // ==========================================
    // 測定ユーティリティ
    // ==========================================
    private static (long AllocatedBytes, double ElapsedUs, int ResultCount) Measure(Func<Dictionary<int, int>> action, int warmup = 5, int iterations = 100)
    {
        for (int i = 0; i < warmup; i++)
        {
            action();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        var res = action();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }
        sw.Stop();
        double elapsedUs = (sw.Elapsed.TotalMicroseconds) / iterations;

        return (allocated, elapsedUs, res.Count);
    }

    [Fact]
    public void Benchmark_DirectSpanPipelineToDictionary_CompareWithSystemLinqAndIdeal()
    {
        var testSizes = new[] { 1_000, 100_000 };
        var results = new List<BenchmarkResult>();

        foreach (var size in testSizes)
        {
            var array = Enumerable.Range(1, size).ToArray();
            int skip = size / 4;
            int take = size / 2;
            int skipLast = size / 5;
            int takeLast = size / 3;

            // 1. Skip -> Take -> ToDictionary
            {
                var (allocSys, timeSys, countSys) = Measure(() => RunSkipTakeToDict_SystemLinq(array, skip, take));
                var (allocIdeal, timeIdeal, countIdeal) = Measure(() => RunSkipTakeToDict_Ideal(array, skip, take));
                var (allocFLinq, timeFLinq, countFLinq) = Measure(() => RunSkipTakeToDict_FLinq(array, skip, take));

                results.Add(new("1. System.Linq", "Skip -> Take -> ToDict", size, allocSys, timeSys, countSys));
                results.Add(new("2. Ideal Loop", "Skip -> Take -> ToDict", size, allocIdeal, timeIdeal, countIdeal));
                results.Add(new("3. FLinq Pipeline", "Skip -> Take -> ToDict", size, allocFLinq, timeFLinq, countFLinq));

                Assert.Equal(countSys, countIdeal);
                Assert.Equal(countSys, countFLinq);
                Assert.Equal(allocIdeal, allocFLinq);
            }

            // 2. SkipLast -> TakeLast -> ToDictionary
            {
                var (allocSys, timeSys, countSys) = Measure(() => RunTrailingSliceToDict_SystemLinq(array, skipLast, takeLast));
                var (allocIdeal, timeIdeal, countIdeal) = Measure(() => RunTrailingSliceToDict_Ideal(array, skipLast, takeLast));
                var (allocFLinq, timeFLinq, countFLinq) = Measure(() => RunTrailingSliceToDict_FLinq(array, skipLast, takeLast));

                results.Add(new("1. System.Linq", "SkipLast -> TakeLast -> ToDict", size, allocSys, timeSys, countSys));
                results.Add(new("2. Ideal Loop", "SkipLast -> TakeLast -> ToDict", size, allocIdeal, timeIdeal, countIdeal));
                results.Add(new("3. FLinq Pipeline", "SkipLast -> TakeLast -> ToDict", size, allocFLinq, timeFLinq, countFLinq));

                Assert.Equal(countSys, countIdeal);
                Assert.Equal(countSys, countFLinq);
                Assert.Equal(allocIdeal, allocFLinq);
            }
        }

        output.WriteLine("==================================================================================================================");
        output.WriteLine($"| {"方式",-20} | {"パターン",-30} | {"要素数",8} | {"Allocated",14} | {"時間 (μs)",12} |");
        output.WriteLine("==================================================================================================================");
        foreach (var r in results)
        {
            output.WriteLine($"| {r.Method,-18} | {r.Pattern,-28} | {r.ElementCount,8:N0} | {r.AllocatedBytes,12:N0} B | {r.ElapsedUs,10:F2} μs |");
        }
        output.WriteLine("==================================================================================================================");
    }
}
