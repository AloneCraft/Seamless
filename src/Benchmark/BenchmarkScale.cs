using Tsavorite.Stores;

namespace BenchmarkExe.Benchmarks;

/// <summary>
/// Global scale factor for per-method <c>const Count</c> values in benchmarks.
/// Each benchmark method (or switch-case operation) defines a base <c>Count</c> chosen so
/// that one iteration — excluding method-level init/teardown — runs for roughly 10 seconds
/// at <see cref="Scale"/> == 1. Increase to run longer / more stable, decrease to run faster.
/// </summary>
public static class BenchmarkScale {
    /// <summary>Multiplier applied to every benchmark method's base <c>Count</c>.</summary>
    public const int Scale = 1;

    /// <summary>Returns <paramref name="baseCount"/> * <see cref="Scale"/> with a floor of 1.</summary>
    public static int Apply(int baseCount) {
        var scaled = baseCount*Scale;
        return scaled<1 ? 1 : scaled;
    }
}

internal static class BenchmarkHashStoreSizing {
    public static long GetIndexSize(int count) {
        return HashStore.GetIndexSizeForCapacity(checked((long)count*2),2048L);
    }

    public static long GetMemorySize(long requiredSize) {
        return RoundUpToPowerOfTwo(4L*1024*1024,requiredSize);
    }

    private static long RoundUpToPowerOfTwo(long minimum,long requiredSize) {
        var targetSize = Math.Max(minimum,requiredSize);
        long size = minimum;
        while(size<targetSize) {
            size<<=1;
        }

        return size;
    }

}
