using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class DirectSpanPipelineElementAtBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedNs, int Result);

    // ==========================================
    // 1. Skip -> Take -> ElementAt
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunSkipTakeElementAt_SystemLinq(int[] array, int skip, int take, int index)
    {
        return array.Skip(skip).Take(take).ElementAt(index);
    }

    private static int RunSkipTakeElementAt_Ideal(int[] array, int skip, int take, int index)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        if ((uint)index >= (uint)length) throw new ArgumentOutOfRangeException(nameof(index));
        return span[startIndex + index];
    }

    private static int RunSkipTakeElementAt_FLinq(int[] array, int skip, int take, int index)
    {
        return array.Skip(skip).Take(take).ElementAt(index);
    }

    // ==========================================
    // 2. SkipLast -> TakeLast -> ElementAt
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunTrailingSliceElementAt_SystemLinq(int[] array, int skipLast, int takeLast, int index)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).ElementAt(index);
    }

    private static int RunTrailingSliceElementAt_Ideal(int[] array, int skipLast, int takeLast, int index)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = 0;
        int endIndex = span.Length;
        endIndex = Math.Max(startIndex, endIndex - Math.Max(0, skipLast));
        startIndex = Math.Max(startIndex, endIndex - Math.Max(0, takeLast));
        int length = Math.Max(0, endIndex - startIndex);
        if ((uint)index >= (uint)length) throw new ArgumentOutOfRangeException(nameof(index));
        return span[startIndex + index];
    }

    private static int RunTrailingSliceElementAt_FLinq(int[] array, int skipLast, int takeLast, int index)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).ElementAt(index);
    }

    // ==========================================
    // 3. Skip -> Take -> Select -> ElementAt
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunSkipTakeSelectElementAt_SystemLinq(int[] array, int skip, int take, int index)
    {
        return array.Skip(skip).Take(take).Select(x => x * 10).ElementAt(index);
    }

    private static int RunSkipTakeSelectElementAt_Ideal(int[] array, int skip, int take, int index)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        if ((uint)index >= (uint)length) throw new ArgumentOutOfRangeException(nameof(index));
        return span[startIndex + index] * 10;
    }

    private static int RunSkipTakeSelectElementAt_FLinq(int[] array, int skip, int take, int index)
    {
        return array.Skip(skip).Take(take).Select(x => x * 10).ElementAt(index);
    }

    [Fact]
    public void Benchmark_DirectSpanPipelineElementAt_CompareWithSystemLinqAndIdeal()
    {
        const int elementCount = 100_000;
        const int skip = 5_000;
        const int take = 40_000;
        const int skipLast = 5_000;
        const int takeLast = 40_000;
        const int targetIndex = 20_000;
        const int iterations = 10_000;

        var sourceArray = new int[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            sourceArray[i] = i;
        }

        // Correctness verification
        var resSys1 = RunSkipTakeElementAt_SystemLinq(sourceArray, skip, take, targetIndex);
        var resIdeal1 = RunSkipTakeElementAt_Ideal(sourceArray, skip, take, targetIndex);
        var resFlinq1 = RunSkipTakeElementAt_FLinq(sourceArray, skip, take, targetIndex);
        Assert.Equal(resSys1, resIdeal1);
        Assert.Equal(resSys1, resFlinq1);

        var resSys2 = RunTrailingSliceElementAt_SystemLinq(sourceArray, skipLast, takeLast, targetIndex);
        var resIdeal2 = RunTrailingSliceElementAt_Ideal(sourceArray, skipLast, takeLast, targetIndex);
        var resFlinq2 = RunTrailingSliceElementAt_FLinq(sourceArray, skipLast, takeLast, targetIndex);
        Assert.Equal(resSys2, resIdeal2);
        Assert.Equal(resSys2, resFlinq2);

        var resSys3 = RunSkipTakeSelectElementAt_SystemLinq(sourceArray, skip, take, targetIndex);
        var resIdeal3 = RunSkipTakeSelectElementAt_Ideal(sourceArray, skip, take, targetIndex);
        var resFlinq3 = RunSkipTakeSelectElementAt_FLinq(sourceArray, skip, take, targetIndex);
        Assert.Equal(resSys3, resIdeal3);
        Assert.Equal(resSys3, resFlinq3);

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            _ = RunSkipTakeElementAt_SystemLinq(sourceArray, skip, take, targetIndex);
            _ = RunSkipTakeElementAt_Ideal(sourceArray, skip, take, targetIndex);
            _ = RunSkipTakeElementAt_FLinq(sourceArray, skip, take, targetIndex);
            _ = RunTrailingSliceElementAt_SystemLinq(sourceArray, skipLast, takeLast, targetIndex);
            _ = RunTrailingSliceElementAt_Ideal(sourceArray, skipLast, takeLast, targetIndex);
            _ = RunTrailingSliceElementAt_FLinq(sourceArray, skipLast, takeLast, targetIndex);
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

        // 1. Skip -> Take -> ElementAt
        Measure("System.Linq", "Skip->Take->ElementAt", () => RunSkipTakeElementAt_SystemLinq(sourceArray, skip, take, targetIndex));
        Measure("Ideal", "Skip->Take->ElementAt", () => RunSkipTakeElementAt_Ideal(sourceArray, skip, take, targetIndex));
        Measure("FLinq (O(1))", "Skip->Take->ElementAt", () => RunSkipTakeElementAt_FLinq(sourceArray, skip, take, targetIndex));

        // 2. SkipLast -> TakeLast -> ElementAt
        Measure("System.Linq", "SkipLast->TakeLast->ElementAt", () => RunTrailingSliceElementAt_SystemLinq(sourceArray, skipLast, takeLast, targetIndex));
        Measure("Ideal", "SkipLast->TakeLast->ElementAt", () => RunTrailingSliceElementAt_Ideal(sourceArray, skipLast, takeLast, targetIndex));
        Measure("FLinq (O(1))", "SkipLast->TakeLast->ElementAt", () => RunTrailingSliceElementAt_FLinq(sourceArray, skipLast, takeLast, targetIndex));

        // 3. Skip -> Take -> Select -> ElementAt
        Measure("System.Linq", "Skip->Take->Select->ElementAt", () => RunSkipTakeSelectElementAt_SystemLinq(sourceArray, skip, take, targetIndex));
        Measure("Ideal", "Skip->Take->Select->ElementAt", () => RunSkipTakeSelectElementAt_Ideal(sourceArray, skip, take, targetIndex));
        Measure("FLinq (O(1))", "Skip->Take->Select->ElementAt", () => RunSkipTakeSelectElementAt_FLinq(sourceArray, skip, take, targetIndex));

        output.WriteLine("| Pattern | Method | Execution Time | Allocated Bytes | Result |");
        output.WriteLine("| :--- | :--- | :--- | :--- | :--- |");
        foreach (var r in results)
        {
            output.WriteLine($"| {r.Pattern} | {r.Method} | {r.ElapsedNs:F2} ns | {r.AllocatedBytes:N0} B | {r.Result:N0} |");
        }
    }
}
