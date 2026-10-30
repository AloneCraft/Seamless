using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class DirectSpanPipelineImmutableArrayBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedUs, long Checksum);

    // ==========================================
    // 1. Skip -> Take -> ToArray
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] RunSkipTakeToArray_SystemLinq(ImmutableArray<int> array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToArray();
    }

    private static int[] RunSkipTakeToArray_Ideal(ImmutableArray<int> array, int skip, int take)
    {
        ReadOnlySpan<int> span = array.AsSpan();
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        if (length == 0) return Array.Empty<int>();
        var result = GC.AllocateUninitializedArray<int>(length);
        span.Slice(startIndex, length).CopyTo(result);
        return result;
    }

    private static int[] RunSkipTakeToArray_FLinq(ImmutableArray<int> array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToArray();
    }

    // ==========================================
    // 2. SkipLast -> TakeLast -> Sum
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunTrailingSliceSum_SystemLinq(ImmutableArray<int> array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).Sum();
    }

    private static int RunTrailingSliceSum_Ideal(ImmutableArray<int> array, int skipLast, int takeLast)
    {
        ReadOnlySpan<int> span = array.AsSpan();
        int startIndex = 0;
        int endIndex = span.Length;
        endIndex = Math.Max(startIndex, endIndex - Math.Max(0, skipLast));
        startIndex = Math.Max(startIndex, endIndex - Math.Max(0, takeLast));
        int length = Math.Max(0, endIndex - startIndex);
        int sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += span[startIndex + i];
        }
        return sum;
    }

    private static int RunTrailingSliceSum_FLinq(ImmutableArray<int> array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).Sum();
    }

    [Fact]
    public void Benchmark_DirectSpanPipelineImmutableArray_CompareWithSystemLinqAndIdeal()
    {
        const int elementCount = 100_000;
        const int skip = 5_000;
        const int take = 40_000;
        const int skipLast = 5_000;
        const int takeLast = 40_000;
        const int iterations = 100;

        var builder = ImmutableArray.CreateBuilder<int>(elementCount);
        for (int i = 0; i < elementCount; i++)
        {
            builder.Add(i % 10);
        }
        var sourceArray = builder.MoveToImmutable();

        // 正当性検証
        var resSysArr = RunSkipTakeToArray_SystemLinq(sourceArray, skip, take);
        var resIdealArr = RunSkipTakeToArray_Ideal(sourceArray, skip, take);
        var resFlinqArr = RunSkipTakeToArray_FLinq(sourceArray, skip, take);
        Assert.Equal(resSysArr, resIdealArr);
        Assert.Equal(resSysArr, resFlinqArr);

        var resSysSum = RunTrailingSliceSum_SystemLinq(sourceArray, skipLast, takeLast);
        var resIdealSum = RunTrailingSliceSum_Ideal(sourceArray, skipLast, takeLast);
        var resFlinqSum = RunTrailingSliceSum_FLinq(sourceArray, skipLast, takeLast);
        Assert.Equal(resSysSum, resIdealSum);
        Assert.Equal(resSysSum, resFlinqSum);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            _ = RunSkipTakeToArray_SystemLinq(sourceArray, skip, take);
            _ = RunSkipTakeToArray_Ideal(sourceArray, skip, take);
            _ = RunSkipTakeToArray_FLinq(sourceArray, skip, take);
            _ = RunTrailingSliceSum_SystemLinq(sourceArray, skipLast, takeLast);
            _ = RunTrailingSliceSum_Ideal(sourceArray, skipLast, takeLast);
            _ = RunTrailingSliceSum_FLinq(sourceArray, skipLast, takeLast);
        }

        var results = new List<BenchmarkResult>();

        void Measure<T>(string method, string pattern, Func<T> func, Func<T, long> checksumExtractor)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            long cs = 0;
            for (int i = 0; i < iterations; i++)
            {
                var ret = func();
                cs = checksumExtractor(ret);
            }

            sw.Stop();
            long afterAlloc = GC.GetAllocatedBytesForCurrentThread();

            long allocatedPerIter = (afterAlloc - beforeAlloc) / iterations;
            double elapsedUs = sw.Elapsed.TotalMicroseconds / iterations;

            results.Add(new BenchmarkResult(method, pattern, elementCount, allocatedPerIter, elapsedUs, cs));
        }

        // 1. Skip -> Take -> ToArray
        Measure("System.Linq", "Skip->Take->ToArray", () => RunSkipTakeToArray_SystemLinq(sourceArray, skip, take), r => r.Length);
        Measure("Ideal", "Skip->Take->ToArray", () => RunSkipTakeToArray_Ideal(sourceArray, skip, take), r => r.Length);
        Measure("FLinq", "Skip->Take->ToArray", () => RunSkipTakeToArray_FLinq(sourceArray, skip, take), r => r.Length);

        // 2. SkipLast -> TakeLast -> Sum
        Measure("System.Linq", "SkipLast->TakeLast->Sum", () => RunTrailingSliceSum_SystemLinq(sourceArray, skipLast, takeLast), r => r);
        Measure("Ideal", "SkipLast->TakeLast->Sum", () => RunTrailingSliceSum_Ideal(sourceArray, skipLast, takeLast), r => r);
        Measure("FLinq", "SkipLast->TakeLast->Sum", () => RunTrailingSliceSum_FLinq(sourceArray, skipLast, takeLast), r => r);

        output.WriteLine("| Pattern | Method | Execution Time | Allocated Bytes | Result |");
        output.WriteLine("| :--- | :--- | :--- | :--- | :--- |");
        foreach (var r in results)
        {
            output.WriteLine($"| {r.Pattern} | {r.Method} | {r.ElapsedUs:F2} μs | {r.AllocatedBytes:N0} B | {r.Checksum:N0} |");
        }
    }
}
