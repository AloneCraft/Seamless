using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

public static class ベンチマークワーカーランタイム {
    private const int ベンチマーク起動回数 = 1;
    private const int ベンチマークウォームアップ回数 = 1;
    private const int ベンチマーク展開係数 = 1;
    private const double 最小ウォームアップナノ秒 = 300_000_000d;
    private const double 最大ウォームアップナノ秒 = 1_024_000_000d;

    public static IConfig 設定を作成(string 成果物フォルダー,int ループ回数 = 1,int ベンチマーク反復回数 = 1) {
        if(ベンチマーク反復回数<=0) {
            throw new ArgumentOutOfRangeException(nameof(ベンチマーク反復回数));
        }

        return ManualConfig
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
    }

    public static bool デバッグベンチマークを許可するか(IConfig 設定) =>
        設定.Options.HasFlag(ConfigOptions.DisableOptimizationsValidator);

    public static bool 使用可能な統計か(double? 平均ナノ秒) =>
        平均ナノ秒 is { } 値&&double.IsFinite(値);

    public static ulong アフィニティマスクを作成(int 論理CPU,int 論理プロセッサ数,int ポインター幅) {
        if(論理プロセッサ数<=0) {
            throw new ArgumentOutOfRangeException(nameof(論理プロセッサ数));
        }
        if(ポインター幅 is not (32 or 64)) {
            throw new ArgumentOutOfRangeException(nameof(ポインター幅));
        }
        if(論理CPU<0||論理CPU>=論理プロセッサ数||論理CPU>=ポインター幅-1) {
            throw new ArgumentOutOfRangeException(nameof(論理CPU));
        }

        return 1UL<<論理CPU;
    }

    public static void スレッドアフィニティ適用結果を検証(ulong 以前のマスク,int 論理CPU,int Win32エラーコード) {
        if(以前のマスク!=0) {
            return;
        }

        throw new System.ComponentModel.Win32Exception(
            Win32エラーコード,
            $"論理CPU {論理CPU} へのSetThreadAffinityMaskに失敗しました。");
    }

    public static long? 一バッチ割当バイト数を正規化(long? 総割当バイト数,long? 総操作回数,int ループ回数) {
        if(総割当バイト数 is null) {
            return null;
        }
        if(ループ回数<=0) {
            throw new ArgumentOutOfRangeException(nameof(ループ回数));
        }
        if(総操作回数 is not >0) {
            return 総割当バイト数;
        }

        return checked((long)Math.Round(
            総割当バイト数.Value/(double)総操作回数.Value*ループ回数,
            MidpointRounding.AwayFromZero));
    }

    public static double ウォームアップ目標ナノ秒を正規化(double 較正閾値ナノ秒) {
        if(!double.IsFinite(較正閾値ナノ秒)||較正閾値ナノ秒<=0d) {
            throw new ArgumentOutOfRangeException(nameof(較正閾値ナノ秒),"較正閾値ナノ秒は有限の正数である必要があります。");
        }

        return Math.Clamp(較正閾値ナノ秒,最小ウォームアップナノ秒,最大ウォームアップナノ秒);
    }

    public static int 次の較正ループ回数を取得(int 現在ループ回数) {
        if(現在ループ回数>=int.MaxValue) {
            throw new InvalidOperationException("較正LoopCountがint.MaxValueへ到達しても目標時間を満たしませんでした。");
        }

        if(現在ループ回数>int.MaxValue/2) {
            return int.MaxValue;
        }

        return 現在ループ回数*2;
    }

    public static 較正結果 ループ回数を較正(Action ベンチマーク呼出,double 較正閾値ナノ秒,int 指定ループ回数) {
        var ウォームアップ目標ナノ秒 = ウォームアップ目標ナノ秒を正規化(較正閾値ナノ秒);
        ベンチマーク呼出();
        直接ウォームアップを実行(ベンチマーク呼出,ウォームアップ目標ナノ秒);

        var ループ回数 = Math.Max(1,指定ループ回数);
        var 時計 = new System.Diagnostics.Stopwatch();
        while(true) {
            時計.Restart();
            バッチを実行(ベンチマーク呼出,ループ回数);
            時計.Stop();
            var バッチナノ秒 = 時計.Elapsed.TotalMilliseconds*1_000_000d;
            if(バッチナノ秒>=較正閾値ナノ秒) {
                return new 較正結果(ループ回数,バッチナノ秒,ウォームアップ目標ナノ秒);
            }

            ループ回数=次の較正ループ回数を取得(ループ回数);
        }
    }

    private static void 直接ウォームアップを実行(Action ベンチマーク呼出,double 目標時間ナノ秒) {
        var 時計 = System.Diagnostics.Stopwatch.StartNew();
        do {
            ベンチマーク呼出();
        } while(時計.Elapsed.TotalMilliseconds*1_000_000d<目標時間ナノ秒);
    }

    private static void バッチを実行(Action ベンチマーク呼出,int ループ回数) {
        for(var 反復番号=0;反復番号<ループ回数;反復番号++) {
            ベンチマーク呼出();
        }
    }
}

public sealed record 較正結果(
    int ループ回数,
    double バッチナノ秒,
    double ウォームアップ目標ナノ秒);
