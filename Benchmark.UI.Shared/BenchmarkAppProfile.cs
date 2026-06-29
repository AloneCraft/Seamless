namespace Benchmark;

internal static class BenchmarkAppProfile {
#if SCALABLE_BENCHMARK
    public const string Title = "Seamless Scalable Benchmark";
    public const string WorkRootName = "Benchmark.Scalable";
#else
    public const string Title = "Seamless Benchmark";
    public const string WorkRootName = "Benchmark";
#endif
}
