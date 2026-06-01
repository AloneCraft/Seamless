using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ZLinq;

namespace Benchmark;

public class LookupBattle
{
    //[Params(8, 20, 50, 100, 1000, 10000)]
    [Params(8, 10000)]
    public int N;

    // [Params(4, 10, 25, 50, 500, 5000)]
    [Params(5000)]
    public int M;

    int[] src = default!;

    [BenchmarkDotNet.Attributes.GlobalSetup]
    public void Setup()
    {
        src = Enumerable.Range(1, N).Select(_ => Random.Shared.Next(0, M)).ToArray();
    }

    [Benchmark]
    public ILookup<int, int> SystemLinq()
    {
        return src.ToLookup(x => x);
    }

    [Benchmark]
    public ILookup<int, int> ZLinq()
    {
        return src.AsValueEnumerable().ToLookup(x => x);
    }
}
