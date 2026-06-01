using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Diagnostics.Tracing.Parsers.JScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ZLinq;
using ZLinq.Simd;

namespace Benchmark;

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class SimdSelect
{
    int[] array = Enumerable.Range(1, 100000).ToArray();
    int[] destination = new int[100000];

    public SimdSelect()
    {

    }

    [Benchmark]
    public void ZLinqSelectCopyTo()
    {
        array.AsValueEnumerable().Select(x => x * 3).CopyTo(destination);
    }

    [Benchmark]
    public void ZLinqVectorizableSelectCopyTo()
    {
        array.AsVectorizable().Select(x => x * 3, x => x * 3).CopyTo(destination);
    }
}
