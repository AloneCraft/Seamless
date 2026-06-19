using System.Runtime.CompilerServices;
using Tsavorite.Carrier;

namespace Tsavorite.Benchmark.Benchmarks;

public class OptimizationBenchmarkContainer
{
    public virtual int SelectInvariantSum(int[] outer)
        => outer.Select(static value => value + HoistInvariant.Value()).Sum();

    public virtual int SelectManyWhereSum(int[] outer, int[] inner, int threshold)
        => outer.SelectMany(_ => inner).Where(value => value > threshold).Sum();

    public virtual int JoinSum(int[] outer, int[] inner)
        => outer.Join(inner, static o => o % 1000, static i => i, static (o, i) => o + i).Sum();
}

internal static class HoistInvariant
{
    private static int seed = 3;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Value() => (seed * 7) + 1;
}