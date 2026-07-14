namespace Common;

// Parameters used when lightweight benchmarks are executed as tests.
internal static class BenchmarkParameters {
    public static int RunCount { get; } = Read("SEAMLESS_BENCHMARK_RUN_COUNT",1,1);
    public static int WarmupCount { get; } = Read("SEAMLESS_BENCHMARK_WARMUP_COUNT",0,0);
    private static int Read(string name,int fallback,int min) {
        var raw = Environment.GetEnvironmentVariable(name);
        if(int.TryParse(raw,out var value)&&value>=min) {
            return value;
        }
        return fallback;
    }
}
