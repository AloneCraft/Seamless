using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class DirectSpanPipelineMaterializeBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedUs, int OutputLength);

    // ==========================================
    // 1. Skip -> Take -> ToArray
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] RunSkipTakeToArray_SystemLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToArray();
    }

    private static int[] RunSkipTakeToArray_Ideal(int[] array, int skip, int take)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        if (length == 0) return Array.Empty<int>();
        var result = GC.AllocateUninitializedArray<int>(length);
        span.Slice(startIndex, length).CopyTo(result);
        return result;
    }

    private static int[] RunSkipTakeToArray_FLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToArray();
    }

    // ==========================================
    // 2. SkipLast -> TakeLast -> ToArray
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] RunTrailingSliceToArray_SystemLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).ToArray();
    }

    private static int[] RunTrailingSliceToArray_Ideal(int[] array, int skipLast, int takeLast)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = 0;
        int endIndex = span.Length;
        endIndex = Math.Max(startIndex, endIndex - Math.Max(0, skipLast));
        startIndex = Math.Max(startIndex, endIndex - Math.Max(0, takeLast));
        int length = Math.Max(0, endIndex - startIndex);
        if (length == 0) return Array.Empty<int>();
        var result = GC.AllocateUninitializedArray<int>(length);
        span.Slice(startIndex, length).CopyTo(result);
        return result;
    }

    private static int[] RunTrailingSliceToArray_FLinq(int[] array, int skipLast, int takeLast)
    {
        return array.SkipLast(skipLast).TakeLast(takeLast).ToArray();
    }

    // ==========================================
    // 3. Skip -> Take -> ToList
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> RunSkipTakeToList_SystemLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToList();
    }

    private static List<int> RunSkipTakeToList_Ideal(int[] array, int skip, int take)
    {
        ReadOnlySpan<int> span = array;
        int startIndex = Math.Min(span.Length, Math.Max(0, skip));
        int endIndex = Math.Min(span.Length, startIndex + Math.Max(0, take));
        int length = Math.Max(0, endIndex - startIndex);
        var result = new List<int>(length);
        CollectionsMarshal.SetCount(result, length);
        span.Slice(startIndex, length).CopyTo(CollectionsMarshal.AsSpan(result));
        return result;
    }

    private static List<int> RunSkipTakeToList_FLinq(int[] array, int skip, int take)
    {
        return array.Skip(skip).Take(take).ToList();
    }

    [Fact]
    public void Benchmark_DirectSpanPipelineMaterialize_CompareWithSystemLinqAndIdeal()
    {
        const int elementCount = 100_000;
        const int skip = 5_000;
        const int take = 40_000;
        const int skipLast = 5_000;
        const int takeLast = 40_000;
        const int iterations = 100;

        var sourceArray = new int[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            sourceArray[i] = i;
        }

        // Correctness verification
        var resSysArr = RunSkipTakeToArray_SystemLinq(sourceArray, skip, take);
        var resIdealArr = RunSkipTakeToArray_Ideal(sourceArray, skip, take);
        var resFlinqArr = RunSkipTakeToArray_FLinq(sourceArray, skip, take);
        Assert.Equal(resSysArr, resIdealArr);
        Assert.Equal(resSysArr, resFlinqArr);

        var resSysTrail = RunTrailingSliceToArray_SystemLinq(sourceArray, skipLast, takeLast);
        var resIdealTrail = RunTrailingSliceToArray_Ideal(sourceArray, skipLast, takeLast);
        var resFlinqTrail = RunTrailingSliceToArray_FLinq(sourceArray, skipLast, takeLast);
        Assert.Equal(resSysTrail, resIdealTrail);
        Assert.Equal(resSysTrail, resFlinqTrail);

        var resSysList = RunSkipTakeToList_SystemLinq(sourceArray, skip, take);
        var resIdealList = RunSkipTakeToList_Ideal(sourceArray, skip, take);
        var resFlinqList = RunSkipTakeToList_FLinq(sourceArray, skip, take);
        Assert.Equal(resSysList, resIdealList);
        Assert.Equal(resSysList, resFlinqList);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            _ = RunSkipTakeToArray_SystemLinq(sourceArray, skip, take);
            _ = RunSkipTakeToArray_Ideal(sourceArray, skip, take);
            _ = RunSkipTakeToArray_FLinq(sourceArray, skip, take);
            _ = RunTrailingSliceToArray_SystemLinq(sourceArray, skipLast, takeLast);
            _ = RunTrailingSliceToArray_Ideal(sourceArray, skipLast, takeLast);
            _ = RunTrailingSliceToArray_FLinq(sourceArray, skipLast, takeLast);
            _ = RunSkipTakeToList_SystemLinq(sourceArray, skip, take);
            _ = RunSkipTakeToList_Ideal(sourceArray, skip, take);
            _ = RunSkipTakeToList_FLinq(sourceArray, skip, take);
        }

        var results = new List<BenchmarkResult>();

        void Measure<T>(string method, string pattern, Func<T> func, Func<T, int> lenExtractor)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            int len = 0;
            for (int i = 0; i < iterations; i++)
            {
                var ret = func();
                len = lenExtractor(ret);
            }

            sw.Stop();
            long afterAlloc = GC.GetAllocatedBytesForCurrentThread();

            long allocatedPerIter = (afterAlloc - beforeAlloc) / iterations;
            double elapsedUs = sw.Elapsed.TotalMicroseconds / iterations;

            results.Add(new BenchmarkResult(method, pattern, elementCount, allocatedPerIter, elapsedUs, len));
        }

        // 1. Skip -> Take -> ToArray
        Measure("System.Linq", "Skip->Take->ToArray", () => RunSkipTakeToArray_SystemLinq(sourceArray, skip, take), r => r.Length);
        Measure("Ideal", "Skip->Take->ToArray", () => RunSkipTakeToArray_Ideal(sourceArray, skip, take), r => r.Length);
        Measure("FLinq", "Skip->Take->ToArray", () => RunSkipTakeToArray_FLinq(sourceArray, skip, take), r => r.Length);

        // 2. SkipLast -> TakeLast -> ToArray
        Measure("System.Linq", "SkipLast->TakeLast->ToArray", () => RunTrailingSliceToArray_SystemLinq(sourceArray, skipLast, takeLast), r => r.Length);
        Measure("Ideal", "SkipLast->TakeLast->ToArray", () => RunTrailingSliceToArray_Ideal(sourceArray, skipLast, takeLast), r => r.Length);
        Measure("FLinq", "SkipLast->TakeLast->ToArray", () => RunTrailingSliceToArray_FLinq(sourceArray, skipLast, takeLast), r => r.Length);

        // 3. Skip -> Take -> ToList
        Measure("System.Linq", "Skip->Take->ToList", () => RunSkipTakeToList_SystemLinq(sourceArray, skip, take), r => r.Count);
        Measure("Ideal", "Skip->Take->ToList", () => RunSkipTakeToList_Ideal(sourceArray, skip, take), r => r.Count);
        Measure("FLinq", "Skip->Take->ToList", () => RunSkipTakeToList_FLinq(sourceArray, skip, take), r => r.Count);

        output.WriteLine("| Pattern | Method | Execution Time | Allocated Bytes | Length |");
        output.WriteLine("| :--- | :--- | :--- | :--- | :--- |");
        foreach (var r in results)
        {
            output.WriteLine($"| {r.Pattern} | {r.Method} | {r.ElapsedUs:F2} μs | {r.AllocatedBytes:N0} B | {r.OutputLength:N0} |");
        }
    }
}
