using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using System.Runtime.CompilerServices;
using ZLinq;
using ZLinq.Linq;

namespace Benchmark;

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class AsValueEnumerableBench
{
    int[] sourceArray;
    IEnumerable<int> sourceIEnumerable;
    IList<int> sourceIList;
    IReadOnlyList<int> sourceIReadOnlyList;

    public AsValueEnumerableBench()
    {
        sourceArray = Enumerable.Range(1, 10000).ToArray();
        sourceIEnumerable = sourceArray;
        sourceIList = sourceArray;
        sourceIReadOnlyList = sourceArray;
    }

    [Benchmark]
    public void SystemLinq()
    {
        var seq = sourceArray
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [Benchmark]
    public void ZLinqArray()
    {
        var seq = sourceArray
            .AsValueEnumerable()
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [Benchmark]
    public void ZLinqIEnumerable()
    {
        var seq = sourceIEnumerable
            .AsValueEnumerable()
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [Benchmark]
    public void ZLinqIList()
    {
        var seq = sourceIList
            .AsValueEnumerable()
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [Benchmark]
    public void ZLinqIReadOnlyList()
    {
        var seq = sourceIReadOnlyList
            .AsValueEnumerable()
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {
            Do(item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    static void Do(int x) { }
}
