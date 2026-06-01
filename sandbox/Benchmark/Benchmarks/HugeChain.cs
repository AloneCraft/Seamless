using BenchmarkDotNet.Attributes;
using ZLinq;
using ZLinq.Linq;
namespace Benchmark;

public class HugeChain
{
    int[] arr = default!;
    int __;

    [Params(10, 100, 1000, 10000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        arr = Enumerable.Range(1, N).ToArray();
    }


    [Benchmark]
    public void Linq()
    {
        foreach (var x in
        arr
            .Select(x => x * x)
            .Where(x => x > 10)
        )
        {
            __ = x;
        }
        ;
    }

    [Benchmark]
    public void ZLinq()
    {
        foreach (var x in
        arr.AsValueEnumerable()
            .Select(x => x * x)
            .Where(x => x > 10)
        )
        {
            __ = x;
        }
        ;
    }

    [Benchmark]
    public void BigLinq()
    {
        foreach (var x in
        arr
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
        )
        {
            __ = x;
        }
        ;
    }

    [Benchmark]
    public void BigZLinq()
    {
        foreach (var x in
        arr.AsValueEnumerable()
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
        )
        {
            __ = x;
        }
        ;
    }

    [Benchmark]
    public void HugeLinq()
    {
        foreach (var x in
        arr
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
        )
        {
            __ = x;
        }
        ;
    }

    [Benchmark]
    public void HugeZLinq()
    {
        foreach (var x in
        arr.AsValueEnumerable()
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
            .Select(x => x * x)
            .Where(x => x > 10)
        )
        {
            __ = x;
        }
        ;
    }
}
