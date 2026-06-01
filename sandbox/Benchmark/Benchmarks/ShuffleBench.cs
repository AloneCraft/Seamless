using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Runtime.InteropServices;
using ZLinq;

namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ShuffleBench
{
    [Params(/*10, 100,*/ 1000, /*10000,*/ 100000)]
    public int N;

    [Params(10, 100, 1000, 100000)]
    public int M;

    int[] array = default!;

    [GlobalSetup]
    public void Setup()
    {
        array = Enumerable.Range(0, N).ToArray();
    }

#if NET10_0_OR_GREATER
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [BenchmarkCategory(Categories.Filters.NET10_0_OR_GREATER)]
    public int ShuffleTakeLinq()
    {
        var src = array.Shuffle().Take(M);
        int i = 0;
        using var e = src.GetEnumerator();
        while (e.MoveNext())
        {
            i++;
            _ = e.Current;
        }
        return i;
    }
#endif

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int ShuffleTakeZL()
    {
        var src = array.AsValueEnumerable().Shuffle().Take(M);
        int i = 0;
        using var e = src.Enumerator;
        while (e.TryGetNext(out var item))
        {
            i++;
        }
        return i;
    }

}
