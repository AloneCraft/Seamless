using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class DirectSpanPipelineMinByMaxByAggregateBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedUs, int ResultValue);

    // ==========================================
    // 1. Skip -> Take -> MinBy
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunSkipTakeMinBy_SystemLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).MinBy(x => x % 100);
    }

    private static int RunSkipTakeMinBy_Ideal(int[] array, int skip, int take)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        if (startIndex >= endIndex) throw new InvalidOperationException();
        int bestVal = span[startIndex];
        int bestKey = bestVal % 100;
        for (int i = startIndex + 1; i < endIndex; i++)
        {
            int val = span[i];
            int key = val % 100;
            if (key < bestKey)
            {
                bestVal = val;
                bestKey = key;
            }
        }
        return bestVal;
    }

    private static int RunSkipTakeMinBy_FLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).MinBy(x => x % 100);
    }

    // ==========================================
    // 2. SkipLast -> TakeLast -> Aggregate (Seed)
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunTrailingSliceAggregate_SystemLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).Aggregate(0, (acc, x) => acc + x);
    }

    private static int RunTrailingSliceAggregate_Ideal(int[] array, int skipLast, int takeLast)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = 0;
        int endIndex = span.Length;
        endIndex = Math.Max(startIndex, endIndex - Math.Max(0, skipLast));
        startIndex = Math.Max(startIndex, endIndex - Math.Max(0, takeLast));
        int acc = 0;
        for (int i = startIndex; i < endIndex; i++)
        {
            acc += span[i];
        }
        return acc;
    }

    private static int RunTrailingSliceAggregate_FLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).Aggregate(0, (acc, x) => acc + x);
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
        var res = action();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }
        sw.Stop();
        double elapsedUs = (sw.Elapsed.TotalMicroseconds) / iterations;

        return (allocated, elapsedUs, res);
    }

    [Fact]
    public void Benchmark_DirectSpanPipelineMinByAggregate_CompareWithSystemLinqAndIdeal()
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

            // 1. MinBy
            {
                var (allocSys, timeSys, resSys) = Measure(() => RunSkipTakeMinBy_SystemLinq(array, skip, take));
                var (allocIdeal, timeIdeal, resIdeal) = Measure(() => RunSkipTakeMinBy_Ideal(array, skip, take));
                var (allocFLinq, timeFLinq, resFLinq) = Measure(() => RunSkipTakeMinBy_FLinq(array, skip, take));

                results.Add(new("1. System.Linq", "Skip -> Take -> MinBy", size, allocSys, timeSys, resSys));
                results.Add(new("2. Ideal Loop", "Skip -> Take -> MinBy", size, allocIdeal, timeIdeal, resIdeal));
                results.Add(new("3. FLinq Pipeline", "Skip -> Take -> MinBy", size, allocFLinq, timeFLinq, resFLinq));

                Assert.Equal(resSys, resIdeal);
                Assert.Equal(resSys, resFLinq);
                Assert.Equal(0, allocIdeal);
                Assert.Equal(0, allocFLinq);
            }

            // 2. Aggregate (Seed)
            {
                var (allocSys, timeSys, resSys) = Measure(() => RunTrailingSliceAggregate_SystemLinq(array, skipLast, takeLast));
                var (allocIdeal, timeIdeal, resIdeal) = Measure(() => RunTrailingSliceAggregate_Ideal(array, skipLast, takeLast));
                var (allocFLinq, timeFLinq, resFLinq) = Measure(() => RunTrailingSliceAggregate_FLinq(array, skipLast, takeLast));

                results.Add(new("1. System.Linq", "SkipLast -> TakeLast -> Aggregate", size, allocSys, timeSys, resSys));
                results.Add(new("2. Ideal Loop", "SkipLast -> TakeLast -> Aggregate", size, allocIdeal, timeIdeal, resIdeal));
                results.Add(new("3. FLinq Pipeline", "SkipLast -> TakeLast -> Aggregate", size, allocFLinq, timeFLinq, resFLinq));

                Assert.Equal(resSys, resIdeal);
                Assert.Equal(resSys, resFLinq);
                Assert.Equal(0, allocIdeal);
                Assert.Equal(0, allocFLinq);
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
