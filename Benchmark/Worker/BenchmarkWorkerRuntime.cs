using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

public static class BenchmarkWorkerRuntime{
    private const int BenchmarkLaunchCount = 1;
    private const int BenchmarkWarmupCount = 1;
    private const int BenchmarkIterationCount = 1;
    private const int BenchmarkInvocationCount = 1;
    private const int BenchmarkUnrollFactor = 1;

    public static IConfig CreateConfig(string artifactFolder) =>
        ManualConfig
            .Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddJob(Job.Default
                .WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithLaunchCount(BenchmarkLaunchCount)
                .WithWarmupCount(BenchmarkWarmupCount)
                .WithIterationCount(BenchmarkIterationCount)
                .WithInvocationCount(BenchmarkInvocationCount)
                .WithUnrollFactor(BenchmarkUnrollFactor))
            .WithArtifactsPath(artifactFolder);

    public static bool AllowsDebugBenchmarking(IConfig config) =>
        config.Options.HasFlag(ConfigOptions.DisableOptimizationsValidator);

    public static bool HasUsableStatistics(double? meanNanoseconds) =>
        meanNanoseconds is { } value&&double.IsFinite(value);
}
