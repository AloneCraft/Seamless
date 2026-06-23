using System.Runtime.CompilerServices;

namespace Benchmark.Shared;

internal static class HoistInvariant{
    private static int seed=3;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Value()=>(seed*7)+1;
}
