using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using GHashSet = System.Collections.Generic.HashSet<int>;

namespace Test.Benchmark;

public class DirectSpanPipelineToHashSetBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedUs, int OutputCount);

    // ==========================================
    // 1. Skip -> Take -> ToHashSet
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static GHashSet RunSkipTakeToHashSet_SystemLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToHashSet();
    }

    private static GHashSet RunSkipTakeToHashSet_Ideal(int[] array, int skip, int take)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        var set = new GHashSet(length);
        for (int i = 0; i < length; i++)
        {
            set.Add(span[startIndex + i]);
        }
        return set;
    }

    private static GHashSet RunSkipTakeToHashSet_FLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToHashSet();
    }

    // ==========================================
    // 2. SkipLast -> TakeLast -> ToHashSet
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static GHashSet RunTrailingSliceToHashSet_SystemLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).ToHashSet();
    }

    private static GHashSet RunTrailingSliceToHashSet_Ideal(int[] array, int skipLast, int takeLast)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = 0;
        int endIndex = span.Length;
        endIndex = Math.Max(startIndex, endIndex - Math.Max(0, skipLast));
        startIndex = Math.Max(startIndex, endIndex - Math.Max(0, takeLast));
        int length = Math.Max(0, endIndex - startIndex);
        var set = new GHashSet(length);
        for (int i = 0; i < length; i++)
        {
            set.Add(span[startIndex + i]);
        }
        return set;
    }

    private static GHashSet RunTrailingSliceToHashSet_FLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).ToHashSet();
    }

    // ==========================================
    // 測定ユーティリティ
    // ==========================================
    private static (long AllocatedBytes, double ElapsedUs, int ResultCount) Measure(Func<GHashSet> action, int warmup = 5, int iterations = 100)
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
    public void Benchmark_DirectSpanPipelineToHashSet_CompareWithSystemLinqAndIdeal()
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

            // 1. Skip -> Take -> ToHashSet
            {
                var (allocSys, timeSys, countSys) = Measure(() => RunSkipTakeToHashSet_SystemLinq(array, skip, take));
                var (allocIdeal, timeIdeal, countIdeal) = Measure(() => RunSkipTakeToHashSet_Ideal(array, skip, take));
                var (allocFLinq, timeFLinq, countFLinq) = Measure(() => RunSkipTakeToHashSet_FLinq(array, skip, take));

                results.Add(new("1. System.Linq", "Skip -> Take -> ToHashSet", size, allocSys, timeSys, countSys));
                results.Add(new("2. Ideal Loop", "Skip -> Take -> ToHashSet", size, allocIdeal, timeIdeal, countIdeal));
                results.Add(new("3. FLinq Pipeline", "Skip -> Take -> ToHashSet", size, allocFLinq, timeFLinq, countFLinq));

                Assert.Equal(countSys, countIdeal);
                Assert.Equal(countSys, countFLinq);
                Assert.Equal(allocIdeal, allocFLinq);
            }

            // 2. SkipLast -> TakeLast -> ToHashSet
            {
                var (allocSys, timeSys, countSys) = Measure(() => RunTrailingSliceToHashSet_SystemLinq(array, skipLast, takeLast));
                var (allocIdeal, timeIdeal, countIdeal) = Measure(() => RunTrailingSliceToHashSet_Ideal(array, skipLast, takeLast));
                var (allocFLinq, timeFLinq, countFLinq) = Measure(() => RunTrailingSliceToHashSet_FLinq(array, skipLast, takeLast));

                results.Add(new("1. System.Linq", "SkipLast -> TakeLast -> ToHashSet", size, allocSys, timeSys, countSys));
                results.Add(new("2. Ideal Loop", "SkipLast -> TakeLast -> ToHashSet", size, allocIdeal, timeIdeal, countIdeal));
                results.Add(new("3. FLinq Pipeline", "SkipLast -> TakeLast -> ToHashSet", size, allocFLinq, timeFLinq, countFLinq));

                Assert.Equal(countSys, countIdeal);
                Assert.Equal(countSys, countFLinq);
                Assert.Equal(allocIdeal, allocFLinq);
            }
        }

        output.WriteLine("==================================================================================================================");
        output.WriteLine($"| {"方式",-20} | {"パターン",-34} | {"要素数",8} | {"Allocated",14} | {"時間 (μs)",12} |");
        output.WriteLine("==================================================================================================================");
        foreach (var r in results)
        {
            output.WriteLine($"| {r.Method,-18} | {r.Pattern,-32} | {r.ElementCount,8:N0} | {r.AllocatedBytes,12:N0} B | {r.ElapsedUs,10:F2} μs |");
        }
        output.WriteLine("==================================================================================================================");
    }
}
