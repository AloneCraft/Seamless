using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using System.Runtime.CompilerServices;
using ZLinq;
using ZLinq.Linq;

namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ReadMeBenchmark2
{
    int[] source = Enumerable.Range(1, 10000).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqWhere()
    {
        var seq = source
            .Where(x => x % 2 == 0);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqWhereTake()
    {
        var seq = source
            .Where(x => x % 2 == 0)
            .Take(10000);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqWhereTakeSelect()
    {
        var seq = source
            .Where(x => x % 2 == 0)
            .Take(10000)
            .Select(x => x * x);

        foreach (var item in seq)
        {
            Do(item);
        }
    }
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqWhere()
    {
        var seq = source.AsValueEnumerable()
            .Where(x => x % 2 == 0);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqWhereTake()
    {
        var seq = source.AsValueEnumerable()
            .Where(x => x % 2 == 0)
            .Take(10000);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqWhereTakeSelect()
    {
        var seq = source.AsValueEnumerable()
            .Where(x => x % 2 == 0)
            .Take(10000)
            .Select(x => x * x);

        foreach (var item in seq)
        {
            Do(item);
        }
    }


    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    static void Do(int x) { }
}
