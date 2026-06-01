using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Cathei.LinqGen;
using SpanLinq;
using StructLinq;
using ZLinq;

namespace Benchmark;

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class IterateBenchmark
{
    int[] array = Enumerable.Range(1, 10000).ToArray();

    public IterateBenchmark()
    {

    }

    [Benchmark]
    public void SystemLinq()
    {
        var seq = array
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {
        }
    }

    [Benchmark]
    public void ZLinq()
    {
        var seq = array.AsValueEnumerable()
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq) { }
    }

    [Benchmark]
    public void ZLinqRaw()
    {
        var seq = array.AsValueEnumerable()
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        using (var e = seq.Enumerator)
        {
            while (e.TryGetNext(out var item))
            {

            }
        }
    }

    [Benchmark]
    public void LinqGen()
    {
        var seq = array.Gen()
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {

        }
    }

    [Benchmark]
    public void LinqAf()
    {
        var seq = global::LinqAF.ArrayExtensionMethods
            .Where(array, x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {

        }
    }

    //[Benchmark]
    //public void StructLinq()
    //{
    //    var seq = array.ToValueEnumerable()
    //        .Select(x => x * 3, x => x)
    //        .Where(x => x % 2 == 0, x => x);

    //    foreach (var item in seq)
    //    {

    //    }
    //}

    [Benchmark]
    public void SpanLinq()
    {
        var seq = array.AsSpan()
            .Where(x => x % 2 == 0)
            .Select(x => x * 3);

        foreach (var item in seq)
        {

        }
    }
}
