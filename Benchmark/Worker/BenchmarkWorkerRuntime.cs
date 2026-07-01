using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

public static class BenchmarkWorkerRuntime{
    private const int BenchmarkLaunchCount = 1;
    private const int BenchmarkWarmupCount = 1;
    private const int BenchmarkIterationCount = 1;
    private const int BenchmarkUnrollFactor = 1;

    public static IConfig CreateConfig(string artifactFolder,int loopCount = 1) =>
        ManualConfig
            .Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithLaunchCount(BenchmarkLaunchCount)
                .WithWarmupCount(BenchmarkWarmupCount)
                .WithIterationCount(BenchmarkIterationCount)
                .WithInvocationCount(Math.Max(1,loopCount))
                .WithUnrollFactor(BenchmarkUnrollFactor))
            .WithArtifactsPath(artifactFolder);

    public static bool AllowsDebugBenchmarking(IConfig config) =>
        config.Options.HasFlag(ConfigOptions.DisableOptimizationsValidator);

    public static bool HasUsableStatistics(double? meanNanoseconds) =>
        meanNanoseconds is { } value&&double.IsFinite(value);
}
