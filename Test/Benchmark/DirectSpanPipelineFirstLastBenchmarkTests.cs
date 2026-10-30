using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class DirectSpanPipelineFirstLastBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedNs, int Result);

    // ==========================================
    // 1. Skip -> Take -> First
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunSkipTakeFirst_SystemLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).First();
    }

    private static int RunSkipTakeFirst_Ideal(int[] array, int skip, int take)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        if (length == 0) throw new InvalidOperationException();
        return span[startIndex];
    }

    private static int RunSkipTakeFirst_FLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).First();
    }

    // ==========================================
    // 2. Skip -> Take -> Last
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunSkipTakeLast_SystemLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).Last();
    }

    private static int RunSkipTakeLast_Ideal(int[] array, int skip, int take)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        if (length == 0) throw new InvalidOperationException();
        return span[endIndex - 1];
    }

    private static int RunSkipTakeLast_FLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).Last();
    }

    // ==========================================
    // 3. SkipLast -> TakeLast -> Last
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunTrailingSliceLast_SystemLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).Last();
    }

    private static int RunTrailingSliceLast_Ideal(int[] array, int skipLast, int takeLast)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = 0;
        int endIndex = span.Length;
        endIndex = Math.Max(startIndex, endIndex - Math.Max(0, skipLast));
        startIndex = Math.Max(startIndex, endIndex - Math.Max(0, takeLast));
        int length = Math.Max(0, endIndex - startIndex);
        if (length == 0) throw new InvalidOperationException();
        return span[endIndex - 1];
    }

    private static int RunTrailingSliceLast_FLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).Last();
    }

    [Fact]
    public void Benchmark_DirectSpanPipelineFirstLast_CompareWithSystemLinqAndIdeal()
    {
        const int elementCount = 100_000;
        const int skip = 5_000;
        const int take = 40_000;
        const int skipLast = 5_000;
        const int takeLast = 40_000;
        const int iterations = 10_000;

        var sourceArray = new int[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            sourceArray[i] = i;
        }

        // Correctness verification
        var resSys1 = RunSkipTakeFirst_SystemLinq(sourceArray, skip, take);
        var resIdeal1 = RunSkipTakeFirst_Ideal(sourceArray, skip, take);
        var resFlinq1 = RunSkipTakeFirst_FLinq(sourceArray, skip, take);
        Assert.Equal(resSys1, resIdeal1);
        Assert.Equal(resSys1, resFlinq1);

        var resSys2 = RunSkipTakeLast_SystemLinq(sourceArray, skip, take);
        var resIdeal2 = RunSkipTakeLast_Ideal(sourceArray, skip, take);
        var resFlinq2 = RunSkipTakeLast_FLinq(sourceArray, skip, take);
        Assert.Equal(resSys2, resIdeal2);
        Assert.Equal(resSys2, resFlinq2);

        var resSys3 = RunTrailingSliceLast_SystemLinq(sourceArray, skipLast, takeLast);
        var resIdeal3 = RunTrailingSliceLast_Ideal(sourceArray, skipLast, takeLast);
        var resFlinq3 = RunTrailingSliceLast_FLinq(sourceArray, skipLast, takeLast);
        Assert.Equal(resSys3, resIdeal3);
        Assert.Equal(resSys3, resFlinq3);

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            _ = RunSkipTakeFirst_SystemLinq(sourceArray, skip, take);
            _ = RunSkipTakeFirst_Ideal(sourceArray, skip, take);
            _ = RunSkipTakeFirst_FLinq(sourceArray, skip, take);
            _ = RunSkipTakeLast_SystemLinq(sourceArray, skip, take);
            _ = RunSkipTakeLast_Ideal(sourceArray, skip, take);
            _ = RunSkipTakeLast_FLinq(sourceArray, skip, take);
            _ = RunTrailingSliceLast_SystemLinq(sourceArray, skipLast, takeLast);
            _ = RunTrailingSliceLast_Ideal(sourceArray, skipLast, takeLast);
            _ = RunTrailingSliceLast_FLinq(sourceArray, skipLast, takeLast);
        }

        var results = new List<BenchmarkResult>();

        void Measure(string method, string pattern, Func<int> func)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            int ret = 0;
            for (int i = 0; i < iterations; i++)
            {
                ret = func();
            }

            sw.Stop();
            long afterAlloc = GC.GetAllocatedBytesForCurrentThread();

            long allocatedPerIter = (afterAlloc - beforeAlloc) / iterations;
            double elapsedNs = (sw.Elapsed.TotalNanoseconds) / iterations;

            results.Add(new BenchmarkResult(method, pattern, elementCount, allocatedPerIter, elapsedNs, ret));
        }

        // 1. Skip -> Take -> First
        Measure("System.Linq", "Skip->Take->First", () => RunSkipTakeFirst_SystemLinq(sourceArray, skip, take));
        Measure("Ideal", "Skip->Take->First", () => RunSkipTakeFirst_Ideal(sourceArray, skip, take));
        Measure("FLinq (O(1))", "Skip->Take->First", () => RunSkipTakeFirst_FLinq(sourceArray, skip, take));

        // 2. Skip -> Take -> Last
        Measure("System.Linq", "Skip->Take->Last", () => RunSkipTakeLast_SystemLinq(sourceArray, skip, take));
        Measure("Ideal", "Skip->Take->Last", () => RunSkipTakeLast_Ideal(sourceArray, skip, take));
        Measure("FLinq (O(1))", "Skip->Take->Last", () => RunSkipTakeLast_FLinq(sourceArray, skip, take));

        // 3. SkipLast -> TakeLast -> Last
        Measure("System.Linq", "SkipLast->TakeLast->Last", () => RunTrailingSliceLast_SystemLinq(sourceArray, skipLast, takeLast));
        Measure("Ideal", "SkipLast->TakeLast->Last", () => RunTrailingSliceLast_Ideal(sourceArray, skipLast, takeLast));
        Measure("FLinq (O(1))", "SkipLast->TakeLast->Last", () => RunTrailingSliceLast_FLinq(sourceArray, skipLast, takeLast));

        output.WriteLine("| Pattern | Method | Execution Time | Allocated Bytes | Result |");
        output.WriteLine("| :--- | :--- | :--- | :--- | :--- |");
        foreach (var r in results)
        {
            output.WriteLine($"| {r.Pattern} | {r.Method} | {r.ElapsedNs:F2} ns | {r.AllocatedBytes:N0} B | {r.Result:N0} |");
        }
    }
}
