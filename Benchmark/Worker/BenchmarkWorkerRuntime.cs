using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

public static class ベンチマークワーカーランタイム {
    private const int ベンチマーク起動回数 = 1;
    private const int ベンチマークウォームアップ回数 = 1;
    private const int ベンチマーク反復回数 = 1;
    private const int ベンチマーク展開係数 = 1;

    public static IConfig 設定を作成(string 成果物フォルダー,int ループ回数 = 1) =>
        ManualConfig
            .Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithLaunchCount(ベンチマーク起動回数)
                .WithWarmupCount(ベンチマークウォームアップ回数)
                .WithIterationCount(ベンチマーク反復回数)
                .WithInvocationCount(Math.Max(1,ループ回数))
                .WithUnrollFactor(ベンチマーク展開係数))
            .WithArtifactsPath(成果物フォルダー);

    public static bool デバッグベンチマークを許可するか(IConfig 設定) =>
        設定.Options.HasFlag(ConfigOptions.DisableOptimizationsValidator);

    public static bool 使用可能な統計か(double? 平均ナノ秒) =>
        平均ナノ秒 is { } 値&&double.IsFinite(値);
}
