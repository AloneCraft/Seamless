using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using ZLinq;
using ZLinq.Linq;

namespace Benchmark;

// Based on: https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-8/
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Net80OptimizedBenchmark
{
    int[] source = default!;

    [GlobalSetup]
    public void Setup()
    {
        source = Enumerable.Range(1, 1000)
                           .Select(_ => Random.Shared.Next(1, 1000))
                           .ToArray();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void SelectToList_SystemLinq()
    {
        _ = source.Select(i => i * 2)
                  .ToList();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void RepeatToList_SystemLinq()
    {
        _ = Enumerable.Repeat((byte)'a', 1024)
                     .ToList();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void RangeSelectToList_SystemLinq()
    {
        _ = Enumerable.Range(0, 1024)
                      .Select(i => i * 2)
                      .ToList();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void SelectToList_ZLinq()
    {
        _ = source.AsValueEnumerable()
            .Select(i => i * 2)
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void RepeatToList_ZLinq()
    {
        _ = ValueEnumerable.Repeat((byte)'a', 1024)
                      .ToList();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void RangeSelectToList_ZLinq()
    {
        _ = ValueEnumerable.Range(0, 1024)
                      .Select(i => i * 2)
                      .ToList();
    }
}
