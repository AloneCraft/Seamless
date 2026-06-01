using BenchmarkDotNet.Attributes;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZLinq.Simd;

namespace Benchmark;

public class VectorizableUpdate
{
    [Params(10000)]
    public int N;

    int[] source = default!;

    [GlobalSetup]
    public void Setup()
    {
        source = Enumerable.Range(0, N).ToArray();
    }

    [Benchmark]
    public int[] For()
    {
        var source = this.source;
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = source[i] * 10;
        }
        return source;
    }

    [Benchmark]
    public int[] VectorizedUpdate()
    {
        source.VectorizedUpdate(static x => x * 10, static x => x * 10);
        return source;
    }

    [Benchmark]
    public int[] InlineSimd()
    {
        InlineSimd(source);
        return source;
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    static void InlineSimd(Span<int> source)
    {
        if (Vector.IsHardwareAccelerated && Vector<int>.IsSupported && source.Length >= Vector<int>.Count)
        {
            var vectors = MemoryMarshal.Cast<int, Vector<int>>(source);
            for (int i = 0; i < vectors.Length; i++)
            {
                vectors[i] = vectors[i] * 10;
            }

            source = source.Slice(vectors.Length * Vector<int>.Count);
        }

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = source[i] * 10;
        }
    }
}
