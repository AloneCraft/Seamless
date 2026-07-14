using System.Runtime.CompilerServices;

namespace Common;

internal static class HoistInvariant {
    private static readonly int seed = 3;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Value() => (seed*7)+1;
}
