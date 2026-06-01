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
public class SimdZip
{
    int[] array1 = Enumerable.Range(1, 100000).ToArray();
    int[] array2 = Enumerable.Range(100000, 100000).ToArray();
    int[] array3 = Enumerable.Range(1000000, 100000).ToArray();
    int[] destination = new int[100000];

    [Benchmark]
    public void ZLinqZipCopyTo()
    {
        array1.AsValueEnumerable().Zip(array2, (x, y) => x + y).CopyTo(destination);
    }

    [Benchmark]
    public void ZLinqVectorizableZipCopyTo()
    {
        array1.AsVectorizable().Zip(array2, (x, y) => x + y, (x, y) => x + y).CopyTo(destination);
    }

    [Benchmark]
    public void ZLinqZip3CopyTo()
    {
        array1.AsValueEnumerable().Zip(array2, array3).Select(x => x.First + x.Second + x.Third).CopyTo(destination);
    }

    [Benchmark]
    public void ZLinqVectorizableZip3CopyTo()
    {
        array1.AsVectorizable().Zip(array2, array3, (x, y, z) => x + y + z, (x, y, z) => x + y + z).CopyTo(destination);
    }
}
