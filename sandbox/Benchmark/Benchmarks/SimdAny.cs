using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZLinq;
using ZLinq.Simd;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Benchmark;

[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByParams)]
public class SimdAny
{
    [Params(32, 128, 1024, 8192, 16384)]
    public int N;

    int target;
    int[] src = default!;

    [BenchmarkDotNet.Attributes.GlobalSetup]
    public void Setup()
    {
        src = Enumerable.Range(1, N).ToArray();
        target = N / 2;
    }

    [Benchmark]
    public bool ForPredicate()
    {
        return AnyForPredicate(src.AsSpan(), x => x > target);
    }

    [Benchmark]
    public bool SystemLinqAny()
    {
        return src.Any(x => x > target);
    }

    [Benchmark]
    public bool ZLinqAsVectorizableAny()
    {
        return src.AsVectorizable()
            .Any(x => Vector.GreaterThanAll(x, new(target)), x => x > target);
    }

    //[Benchmark]
    //public bool ZLinqAsVectorizableX()
    //{
    //    return src.AsVectorizable()
    //        .Any(x => Vector.GreaterThanAny(x, new(9800)), VectorBoundaryMode.ZeroPadding);
    //}

    //[Benchmark]
    //public bool ZLinqAsVectorizableOp()
    //{
    //    return src.AsVectorizable()
    //        .Any(VectorComparer.GreaterThan(9800));
    //}

    //[Benchmark]
    //public bool ZLinqAsVectorizable2()
    //{
    //    return src.AsVectorizable()
    //        .Any2(x => Vector.GreaterThanAny(x, new(9800)), x => x > 9800);
    //}

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool AnyFor(ReadOnlySpan<int> span)
    {
        foreach (var item in span)
        {
            if (item > 9800)
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool AnyForPredicate(ReadOnlySpan<int> span, Func<int, bool> predicate)
    {
        foreach (var item in span)
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool AnySimd<T>(ReadOnlySpan<int> span)
    {
        ref var current = ref MemoryMarshal.GetReference(span);
        ref var end = ref Unsafe.Add(ref current, span.Length);

        if (Vector.IsHardwareAccelerated && span.Length >= Vector<T>.Count)
        {
            ref var to = ref Unsafe.Subtract(ref end, Vector<T>.Count);
#if NET9_0_OR_GREATER
            var state = Vector.Create(9800);
#else
            var state = new Vector<int>(9800);
#endif
            do
            {
                var data = Vector.LoadUnsafe(ref current);
                if (Vector.GreaterThanAny(data, state))
                {
                    return true;
                }
                current = ref Unsafe.Add(ref current, Vector<T>.Count);
            } while (!Unsafe.IsAddressGreaterThan(ref current, ref to));
        }

        // iterate rest
        while (Unsafe.IsAddressLessThan(ref current, ref end))
        {
            if (current > 9800)
            {
                return true;
            }
            current = ref Unsafe.Add(ref current, 1);
        }

        return false;
    }
}
