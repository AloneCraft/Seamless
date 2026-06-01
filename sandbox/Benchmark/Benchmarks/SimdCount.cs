using BenchmarkDotNet.Attributes;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using ZLinq.Simd;

namespace Benchmark;

public class SimdCount
{
    int[] source = default!;

    [GlobalSetup]
    public void Setup()
    {
        source = Enumerable.Range(0, 10000).Select(x => Random.Shared.Next(0, 10000)).ToArray();
    }

    [Benchmark]
    public int LinqCount()
    {
        return source.Count(x => x > 5000);
    }

    [Benchmark]
    public int VectorizableCount()
    {
        return source.AsVectorizable().Count(x => Vector.GreaterThan(x, new(5000)), x => x > 5000);
    }

    [Benchmark]
    public int SimdInline()
    {
        return SimdCompare<int>(source, Vector256.Create(5000));
    }

    static int SimdCompare<T>(ReadOnlySpan<T> src, Vector256<T> compare)
        where T : struct, IComparisonOperators<T, int, bool>
    {
        var count = 0;
        if (Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported && src.Length >= Vector256<T>.Count)
        {
            var vectors = MemoryMarshal.Cast<T, Vector256<T>>(src);

            foreach (var item in vectors)
            {
                var compareResult = Vector256.GreaterThan(item, compare);
                var bits = Vector256.ExtractMostSignificantBits(compareResult);
                var popCount = BitOperations.PopCount(bits);
                count += popCount;
            }

            src = src.Slice(vectors.Length * Vector256<T>.Count);
        }

        foreach (T item in src)
        {
            if (item > 5000)
            {
                count++;
            }
        }

        return count;
    }
}
