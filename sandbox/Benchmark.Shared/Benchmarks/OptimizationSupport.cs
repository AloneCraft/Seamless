using System.Runtime.CompilerServices;

namespace Seamless.Benchmarks;

internal static class HoistInvariant{
    private static int seed=3;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Value()=>(seed*7)+1;
}
