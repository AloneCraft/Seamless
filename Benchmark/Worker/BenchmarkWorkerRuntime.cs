using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

using System.Diagnostics;
using System.Globalization;
using System.Runtime;

#if NET12_0_OR_GREATER
#error .NET 12以降を対象にしました。dotnet/runtime#129020(RuntimeHelpers.GetMethodCompilationStatus や WaitForFinalCompilationOrCallCountingReadyAsync など、メソッドが最終段階まで昇格したことを待つAPIの提案)が採用されたか確認してください。採用済みなら、ベンチマークワーカーランタイム.ウォームアップを実行 の「JITコンパイル数の静穏と呼出速度の安定」による判定を公式APIによる最終段階の待機へ置き換え、この#errorを削除してください(仕様は Document/UI/BenchmarkMeasurementSpecification.md の「JIT最適化完了待機」)。未採用なら、この条件を NET13_0_OR_GREATER へ更新してください。
#endif

public static class ベンチマークワーカーランタイム {
    private const int ベンチマーク起動回数 = 1;
    private const int ベンチマークウォームアップ回数 = 1;
    private const int ベンチマーク展開係数 = 1;
    private const double 最小ウォームアップナノ秒 = 300_000_000d;
    private const double 最大ウォームアップナノ秒 = 1_024_000_000d;
    // ウォームアップは「呼出しを続けるだけ」ではなく、JITの昇格が落ち着くまで待つ。待機の最小/上限時間。
    public const double 最適化待機最小ミリ秒 = 1000d;
    public const double 最適化待機上限ミリ秒 = 10_000d;
    // JITのコンパイル数(裏の昇格を含む)がこの時間だけ変わらなければ、昇格が落ち着いたとみなす。
    public const double コンパイル静穏ミリ秒 = 200d;
    // 呼出しを続ける時間。これを超えたら1ミリ秒眠って、裏のJITスレッドにCPUを渡す。
    private const double 呼出バーストミリ秒 = 5d;
    // 呼出速度の安定(ドリフト)を見る直近サイクル数と、窓の前半/後半の中央値の許容差。
    public const int 安定窓サイクル数 = 20;
    private const double 安定許容ドリフト = 0.15;
    private const int 初期速度サイクル数 = 5;

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
        var JIT待機 = ウォームアップを実行(ベンチマーク呼出,ウォームアップ目標ナノ秒);

        var ループ回数 = Math.Max(1,指定ループ回数);
        var 時計 = new System.Diagnostics.Stopwatch();
        while(true) {
            時計.Restart();
            バッチを実行(ベンチマーク呼出,ループ回数);
            時計.Stop();
            var バッチナノ秒 = 時計.Elapsed.TotalMilliseconds*1_000_000d;
            if(バッチナノ秒>=較正閾値ナノ秒) {
                return new 較正結果(ループ回数,バッチナノ秒,ウォームアップ目標ナノ秒,JIT待機);
            }

            ループ回数=次の較正ループ回数を取得(ループ回数);
        }
    }

    /// <summary>
    /// 呼出速度(1呼出のナノ秒)が安定しているか。直近の値を前半と後半に分け、中央値の差が前半の許容ドリフト以内なら安定。
    /// 1サイクル(約15ms)ごとの速度は4〜8%ぶれるため、最大/最小の幅で見るとノイズだけで安定と判定されない。
    /// </summary>
    public static bool 速度が安定しているか(IReadOnlyList<double> 直近ナノ秒,int 必要件数) {
        if(必要件数<4||直近ナノ秒.Count<必要件数) {
            return false;
        }

        var 有効 = 直近ナノ秒.Where(static 値 => 値>0d&&double.IsFinite(値)).ToArray();
        if(有効.Length<必要件数) {
            return false;
        }

        var 半分 = 有効.Length/2;
        var 前半 = 中央値(有効[..半分]);
        var 後半 = 中央値(有効[半分..]);
        return Math.Abs(後半-前半)<=前半*安定許容ドリフト;
    }

    /// <summary>ウォームアップを終えて計測へ進んでよいか。最小時間を過ぎ、JITのコンパイル数が静穏で、呼出速度が安定していること。</summary>
    public static bool 最適化待機を終了できるか(double 経過ミリ秒,double コンパイル静穏経過ミリ秒,bool 速度安定か) {
        if(経過ミリ秒<最適化待機最小ミリ秒) {
            return false;
        }
        if(コンパイル静穏経過ミリ秒<コンパイル静穏ミリ秒) {
            return false;
        }

        return 速度安定か;
    }

    /// <summary>
    /// ウォームアップを実行する。呼出しを続けながら、JITの昇格が落ち着くまで(上限あり)待ってから戻る。
    /// 単一CPUに固定されたワーカーで呼出しを休みなく続けると、裏のJITスレッドが動けず最適化前のコードのまま計測に入ることがあるため、
    /// 一定時間ごとに眠ってCPUを渡す。
    /// メソッドが最終段階(Tier1)に達したことは直接確認できない。JIT階層監視のEventListenerで確認する方式は、
    /// 有効にしたプロセスの約半数で対象メソッドの昇格そのものを止めることが実測で分かったため使わない。
    /// </summary>
    public static JIT待機結果 ウォームアップを実行(Action ベンチマーク呼出,double 目標時間ナノ秒) {
        var 最小待機ミリ秒 = Math.Max(最適化待機最小ミリ秒,目標時間ナノ秒/1_000_000d);
        var 上限ミリ秒 = Math.Max(最小待機ミリ秒,最適化待機上限ミリ秒);
        var 時計 = Stopwatch.StartNew();
        var 呼出回数 = 0L;
        var 速度一覧 = new List<double>();
        var 直近速度 = new Queue<double>();
        var 最後のコンパイル数 = JitInfo.GetCompiledMethodCount(false);
        var 最後にコンパイルが増えたミリ秒 = 0d;
        while(true) {
            var バースト = Stopwatch.StartNew();
            var バースト呼出回数 = 0L;
            do {
                ベンチマーク呼出();
                バースト呼出回数++;
            } while(バースト.Elapsed.TotalMilliseconds<呼出バーストミリ秒);
            呼出回数+=バースト呼出回数;
            var 速度 = バースト.Elapsed.TotalMilliseconds*1_000_000d/バースト呼出回数;
            速度一覧.Add(速度);
            直近速度.Enqueue(速度);
            while(直近速度.Count>安定窓サイクル数) {
                直近速度.Dequeue();
            }
            Thread.Sleep(1);

            var 経過ミリ秒 = 時計.Elapsed.TotalMilliseconds;
            var コンパイル数 = JitInfo.GetCompiledMethodCount(false);
            if(コンパイル数!=最後のコンパイル数) {
                最後のコンパイル数=コンパイル数;
                最後にコンパイルが増えたミリ秒=経過ミリ秒;
            }
            var 速度安定か = 速度が安定しているか(直近速度.ToArray(),安定窓サイクル数);
            if(最適化待機を終了できるか(経過ミリ秒,経過ミリ秒-最後にコンパイルが増えたミリ秒,速度安定か)) {
                return 待機結果を作成(JIT待機状態.安定,経過ミリ秒,呼出回数,速度一覧,直近速度);
            }
            if(経過ミリ秒>=上限ミリ秒) {
                return 待機結果を作成(JIT待機状態.時間切れ,経過ミリ秒,呼出回数,速度一覧,直近速度);
            }
        }
    }

    private static JIT待機結果 待機結果を作成(JIT待機状態 状態,double 経過ミリ秒,long 呼出回数,List<double> 速度一覧,Queue<double> 直近速度) {
        var 初期 = 中央値(速度一覧.Take(初期速度サイクル数).ToArray());
        var 最終 = 中央値(直近速度.ToArray());
        return new JIT待機結果(状態,経過ミリ秒,呼出回数,初期,最終);
    }

    private static double 中央値(double[] 値一覧) {
        if(値一覧.Length==0) {
            return 0d;
        }

        var 並べ替え = 値一覧.Order().ToArray();
        return 並べ替え[並べ替え.Length/2];
    }

    /// <summary>ログ用の1行に整形する。速度は1呼出あたりの時間で、初期→最終の順(最終が初期より大きく下がっていれば昇格が効いている)。</summary>
    public static string JIT待機ログを整形(JIT待機結果 結果) =>
        "JIT最適化待機: 状態="+結果.状態
        +"、待機="+結果.待機ミリ秒.ToString("F0",CultureInfo.InvariantCulture)+"ms"
        +"、呼出="+結果.呼出回数.ToString(CultureInfo.InvariantCulture)
        +"、1呼出="+(結果.初期ナノ秒/1000d).ToString("F1",CultureInfo.InvariantCulture)+"us→"+(結果.最終ナノ秒/1000d).ToString("F1",CultureInfo.InvariantCulture)+"us";

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
    double ウォームアップ目標ナノ秒,
    JIT待機結果? JIT待機 = null);

public enum JIT待機状態 {
    /// <summary>JITのコンパイル数が落ち着き、呼出速度も安定した。</summary>
    安定,
    /// <summary>上限時間までに落ち着かなかった。最適化前のコードで測る可能性がある。</summary>
    時間切れ,
}

public readonly record struct JIT待機結果(JIT待機状態 状態,double 待機ミリ秒,long 呼出回数,double 初期ナノ秒,double 最終ナノ秒);
