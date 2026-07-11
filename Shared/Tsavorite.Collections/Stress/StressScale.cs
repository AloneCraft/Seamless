#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections.Stress;
#else
namespace Tsavorite.Collections.Stress;
#endif

internal static class StressScale {
    // Scale=1 is the calibrated baseline. Increase for heavier stress runs.
    public const int Scale = 1;

    public static int Apply(int baseCount) {
        var scaled = baseCount*Scale;
        return scaled<1 ? 1 : scaled;
    }
}
