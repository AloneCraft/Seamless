using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Test.Benchmark;

public class SelectManyBenchmarkTests(ITestOutputHelper output)
{
    private record struct BenchmarkResult(string Method, string Pattern, int ElementCount, long AllocatedBytes, double ElapsedUs, long Checksum);

    // ==========================================
    // 1. Array x Array -> ToArray
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] RunArrayArrayToArray_SystemLinq(int[] outer, int[] inner)
    {
        return outer.SelectMany(_ => inner).ToArray();
    }

    private static int[] RunArrayArrayToArray_Ideal(int[] outer, int[] inner)
    {
        var result = GC.AllocateUninitializedArray<int>(outer.Length * inner.Length);
        int destIndex = 0;
        for (int i = 0; i < outer.Length; i++)
        {
            inner.CopyTo(result, destIndex);
            destIndex += inner.Length;
        }
        return result;
    }

    private static int[] RunArrayArrayToArray_FLinq(int[] outer, int[] inner)
    {
        return outer.SelectMany(_ => inner).ToArray();
    }

    // ==========================================
    // 2. List x List -> ToList
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static List<int> RunListListToList_SystemLinq(List<int> outer, List<int> inner)
    {
        return outer.SelectMany(_ => inner).ToList();
    }

    private static List<int> RunListListToList_Ideal(List<int> outer, List<int> inner)
    {
        var result = new List<int>(outer.Count * inner.Count);
        var innerSpan = CollectionsMarshal.AsSpan(inner);
        for (int i = 0; i < outer.Count; i++)
        {
            for (int j = 0; j < innerSpan.Length; j++)
            {
                result.Add(innerSpan[j]);
            }
        }
        return result;
    }

    private static List<int> RunListListToList_FLinq(List<int> outer, List<int> inner)
    {
        return outer.SelectMany(_ => inner).ToList();
    }

    // ==========================================
    // 3. Array x Array + resultSelector -> Sum
    // ==========================================

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int RunArrayArrayResultSelectorSum_SystemLinq(int[] outer, int[] inner)
    {
        return outer.SelectMany(_ => inner, (o, i) => o + i).Sum();
    }

    private static int RunArrayArrayResultSelectorSum_Ideal(int[] outer, int[] inner)
    {
        int sum = 0;
        for (int i = 0; i < outer.Length; i++)
        {
            int o = outer[i];
            for (int j = 0; j < inner.Length; j++)
            {
                sum += o + inner[j];
            }
        }
        return sum;
    }

    private static int RunArrayArrayResultSelectorSum_FLinq(int[] outer, int[] inner)
    {
        return outer.SelectMany(_ => inner, (o, i) => o + i).Sum();
    }

    [Fact]
    public void Benchmark_SelectMany_CompareWithSystemLinqAndIdeal()
    {
        const int outerCount = 64;
        const int innerCount = 1000;
        const int totalElements = outerCount * innerCount;
        const int iterations = 100;

        var outerArray = new int[outerCount];
        for (int i = 0; i < outerCount; i++) outerArray[i] = i;

        var innerArray = new int[innerCount];
        for (int i = 0; i < innerCount; i++) innerArray[i] = i % 10;

        var outerList = new List<int>(outerArray);
        var innerList = new List<int>(innerArray);

        // 正当性検証
        var resSysArr = RunArrayArrayToArray_SystemLinq(outerArray, innerArray);
        var resIdealArr = RunArrayArrayToArray_Ideal(outerArray, innerArray);
        var resFlinqArr = RunArrayArrayToArray_FLinq(outerArray, innerArray);
        Assert.Equal(resSysArr, resIdealArr);
        Assert.Equal(resSysArr, resFlinqArr);

        var resSysList = RunListListToList_SystemLinq(outerList, innerList);
        var resIdealList = RunListListToList_Ideal(outerList, innerList);
        var resFlinqList = RunListListToList_FLinq(outerList, innerList);
        Assert.Equal(resSysList, resIdealList);
        Assert.Equal(resSysList, resFlinqList);

        var resSysSum = RunArrayArrayResultSelectorSum_SystemLinq(outerArray, innerArray);
        var resIdealSum = RunArrayArrayResultSelectorSum_Ideal(outerArray, innerArray);
        var resFlinqSum = RunArrayArrayResultSelectorSum_FLinq(outerArray, innerArray);
        Assert.Equal(resSysSum, resIdealSum);
        Assert.Equal(resSysSum, resFlinqSum);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            _ = RunArrayArrayToArray_SystemLinq(outerArray, innerArray);
            _ = RunArrayArrayToArray_Ideal(outerArray, innerArray);
            _ = RunArrayArrayToArray_FLinq(outerArray, innerArray);
            _ = RunListListToList_SystemLinq(outerList, innerList);
            _ = RunListListToList_Ideal(outerList, innerList);
            _ = RunListListToList_FLinq(outerList, innerList);
            _ = RunArrayArrayResultSelectorSum_SystemLinq(outerArray, innerArray);
            _ = RunArrayArrayResultSelectorSum_Ideal(outerArray, innerArray);
            _ = RunArrayArrayResultSelectorSum_FLinq(outerArray, innerArray);
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

            results.Add(new BenchmarkResult(method, pattern, totalElements, allocatedPerIter, elapsedUs, cs));
        }

        // 1. Array x Array -> ToArray
        Measure("System.Linq", "Array x Array -> ToArray", () => RunArrayArrayToArray_SystemLinq(outerArray, innerArray), r => r.Length);
        Measure("Ideal", "Array x Array -> ToArray", () => RunArrayArrayToArray_Ideal(outerArray, innerArray), r => r.Length);
        Measure("FLinq", "Array x Array -> ToArray", () => RunArrayArrayToArray_FLinq(outerArray, innerArray), r => r.Length);

        // 2. List x List -> ToList
        Measure("System.Linq", "List x List -> ToList", () => RunListListToList_SystemLinq(outerList, innerList), r => r.Count);
        Measure("Ideal", "List x List -> ToList", () => RunListListToList_Ideal(outerList, innerList), r => r.Count);
        Measure("FLinq", "List x List -> ToList", () => RunListListToList_FLinq(outerList, innerList), r => r.Count);

        // 3. Array x Array + resultSelector -> Sum
        Measure("System.Linq", "Array x Array (resultSelector) -> Sum", () => RunArrayArrayResultSelectorSum_SystemLinq(outerArray, innerArray), r => r);
        Measure("Ideal", "Array x Array (resultSelector) -> Sum", () => RunArrayArrayResultSelectorSum_Ideal(outerArray, innerArray), r => r);
        Measure("FLinq", "Array x Array (resultSelector) -> Sum", () => RunArrayArrayResultSelectorSum_FLinq(outerArray, innerArray), r => r);

        output.WriteLine("| Pattern | Method | Execution Time | Allocated Bytes |");
        output.WriteLine("| :--- | :--- | :--- | :--- |");
        foreach (var r in results)
        {
            output.WriteLine($"| {r.Pattern} | {r.Method} | {r.ElapsedUs:F2} μs | {r.AllocatedBytes:N0} B |");
        }
    }
}
