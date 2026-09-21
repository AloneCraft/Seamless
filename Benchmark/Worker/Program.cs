using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

Console.OutputEncoding=Encoding.UTF8;
Console.InputEncoding=Encoding.UTF8;
const string 要素倍率環境変数名 = "SEAMLESS_BENCHMARK_ELEMENT_SCALE";
const string 要素数環境変数名 = "SEAMLESS_BENCHMARK_ELEMENT_COUNT";
const string ループ回数環境変数名 = "SEAMLESS_BENCHMARK_LOOP_COUNT";
const string 準備済み要素数環境変数名 = "SEAMLESS_BENCHMARK_PREPARED_ELEMENT_COUNTS";
const string ランダムソースシード環境変数名 = "SEAMLESS_BENCHMARK_RANDOM_SOURCE_SEED";

var 論理CPU = 必須整数引数を読む(args,"--logical-cpu");
var 実行ルート = 必須文字列引数を読む(args,"--run-root");
Directory.CreateDirectory(実行ルート);
アフィニティを適用(論理CPU);

var プロトコル出力 = Console.Out;
var プロトコルエラー = Console.Error;
var プロトコル書込ゲート = new SemaphoreSlim(1,1);
var 高速タスク一覧 = new ConcurrentBag<Task>();
await プロトコル出力.WriteLineAsync("ready");
await プロトコル出力.FlushAsync();
while(await Console.In.ReadLineAsync() is { } 行) {
    行=UTF8BOMを除去(行);
    if(string.IsNullOrWhiteSpace(行)) {
        continue;
    }

    ワーカータスクメッセージ? タスク = null;
    try {
        タスク=JsonSerializer.Deserialize<ワーカータスクメッセージ>(行);
        if(タスク is null) {
            continue;
        }

        var command = タスク.Command??タスク.コマンド;
        if(string.Equals(command,"shutdown",StringComparison.OrdinalIgnoreCase)) {
            await Task.WhenAll(高速タスク一覧.ToArray());
            return 0;
        }

        if(string.Equals(command,"paired-fast-run",StringComparison.OrdinalIgnoreCase)||string.Equals(command,"profile-paired",StringComparison.OrdinalIgnoreCase)) {
            var result = 演算子対比較を実行(タスク,タスク.割り当て論理CPU??論理CPU);
            await プロトコル書込ゲート.WaitAsync();
            try {
                await プロトコル出力.WriteLineAsync(JsonSerializer.Serialize(result));
                await プロトコル出力.FlushAsync();
            } finally {
                プロトコル書込ゲート.Release();
            }
            continue;
        }
        if(string.Equals(command,"fast-run",StringComparison.OrdinalIgnoreCase)) {
            var 高速タスク = タスク;
            高速タスク一覧.Add(Task.Run(async () => {
                ワーカー結果メッセージ 結果;
                try {
                    結果=高速ベンチマークを実行(高速タスク,高速タスク.割り当て論理CPU??論理CPU,プロトコル出力,プロトコルエラー);
                } catch(Exception 例外) {
                    結果=new ワーカー結果メッセージ(
                        高速タスク.タスクID,
                        "Failed",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        null,
                        null,
                        null,
                        null,
                        1,
                        高速タスク.成果物フォルダー,
                        例外.ToString(),
                        null,
                        string.Empty);
                }
                await プロトコル書込ゲート.WaitAsync();
                try {
                    await プロトコル出力.WriteLineAsync(JsonSerializer.Serialize(結果));
                    await プロトコル出力.FlushAsync();
                } finally {
                    プロトコル書込ゲート.Release();
                }
            }));
            continue;
        }

        var 結果 = ベンチマークを実行(タスク,論理CPU,プロトコル出力,プロトコルエラー);
        await プロトコル書込ゲート.WaitAsync();
        try {
            await プロトコル出力.WriteLineAsync(JsonSerializer.Serialize(結果));
            await プロトコル出力.FlushAsync();
        } finally {
            プロトコル書込ゲート.Release();
        }
    } catch(Exception 例外) {
        var 結果 = new ワーカー結果メッセージ(
            タスク?.タスクID??string.Empty,
            "Failed",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            1,
            タスク?.成果物フォルダー??string.Empty,
            例外.ToString(),
            null,
            string.Empty);
        await プロトコル書込ゲート.WaitAsync();
        try {
            await プロトコル出力.WriteLineAsync(JsonSerializer.Serialize(結果));
            await プロトコル出力.FlushAsync();
        } finally {
            プロトコル書込ゲート.Release();
        }
    }
}

return 0;

static ワーカー結果メッセージ ベンチマークを実行(
    ワーカータスクメッセージ タスク,
    int 論理CPU,
    TextWriter プロトコル出力,
    TextWriter プロトコルエラー) {
    Directory.CreateDirectory(タスク.成果物フォルダー);
    var ログパス = Path.Combine(タスク.成果物フォルダー,"worker.log");

    using var ログ書込 = new StringWriter(CultureInfo.InvariantCulture);
    var 前回要素倍率 = Environment.GetEnvironmentVariable(要素倍率環境変数名);
    var 前回要素数 = Environment.GetEnvironmentVariable(要素数環境変数名);
    var 前回ループ回数 = Environment.GetEnvironmentVariable(ループ回数環境変数名);
    var 前回準備済み要素数 = Environment.GetEnvironmentVariable(準備済み要素数環境変数名);
    var 前回ランダムソースシード = Environment.GetEnvironmentVariable(ランダムソースシード環境変数名);
    Console.SetOut(ログ書込);
    Console.SetError(ログ書込);
    try {
        var アフィニティ適用成功か = アフィニティを適用(論理CPU);
        var ランダムシード = ランダムソースシードを作成(タスク);
        var ループ回数 = Math.Max(1,タスク.ループ回数);
        Environment.SetEnvironmentVariable(要素倍率環境変数名,string.IsNullOrWhiteSpace(タスク.要素倍率) ? "1" : タスク.要素倍率);
        Environment.SetEnvironmentVariable(
            要素数環境変数名,
            タスク.要素数>0 ? タスク.要素数.ToString(CultureInfo.InvariantCulture) : null);
        Environment.SetEnvironmentVariable(ループ回数環境変数名,ループ回数.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable(
            準備済み要素数環境変数名,
            タスク.準備済み要素数.Length>0
                ? string.Join(",",タスク.準備済み要素数.Distinct().Order().Select(static 要素数 => 要素数.ToString(CultureInfo.InvariantCulture)))
                : null);
        Environment.SetEnvironmentVariable(ランダムソースシード環境変数名,ランダムシード.ToString(CultureInfo.InvariantCulture));
        var ベンチマーク型 = ベンチマーク型を解決(タスク.アセンブリ名,タスク.型完全名);
        using var 要素数スコープ = 要素数スコープを開始(ベンチマーク型.Assembly,タスク.要素数,タスク.要素倍率);
        using var ランダムソーススコープ = ランダムソースシードスコープを開始(ベンチマーク型.Assembly,ランダムシード);
        var 引数値一覧 = ベンチマーク引数フォーマッター.オブジェクト一覧に変換(タスク.引数);
        var 結果ダイジェスト = ベンチマーク結果ダイジェストを取得(ベンチマーク型,タスク.メソッド名,引数値一覧);
        if(タスク.開放ループ較正か) {
            var 較正閾値ナノ秒 = タスク.較正閾値ナノ秒
                ??throw new InvalidOperationException("開放ループ較正には較正閾値ナノ秒が必要です。");
            // 開放ループはLoopCountを決める一時プローブであり、その時間・誤差・割当量を最終統計として返さない。
            var プローブメソッド = ベンチマークメソッドを解決(ベンチマーク型,タスク.メソッド名);
            var プローブインスタンス = プローブメソッド.IsStatic ? null : Activator.CreateInstance(ベンチマーク型);
            using var プローブ破棄 = プローブインスタンス as IDisposable;
            var プローブ呼出 = 引数値一覧.Length==0
                ? プローブメソッド.IsStatic
                    ? (Action)プローブメソッド.CreateDelegate(typeof(Action))
                    : (Action)プローブメソッド.CreateDelegate(typeof(Action),プローブインスタンス)
                : () => プローブメソッド.Invoke(プローブインスタンス,引数値一覧);
            var プローブ計測 = ベンチマークワーカーランタイム.ループ回数を較正(プローブ呼出,較正閾値ナノ秒,ループ回数);
            var 実測ループ回数 = プローブ計測.ループ回数;
            Environment.SetEnvironmentVariable(ループ回数環境変数名,実測ループ回数.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("較正プローブ: "+較正ログを整形(プローブ計測));
            var プローブログ = ログ書込.ToString();
            File.WriteAllText(ログパス,プローブログ,Encoding.UTF8);
            return new ワーカー結果メッセージ(
                タスク.タスクID,
                "Completed",
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                0,
                タスク.成果物フォルダー,
                プローブログ,
                ランダムシード,
                結果ダイジェスト,
                実測ループ回数: 実測ループ回数,
                アフィニティ適用成功か: アフィニティ適用成功か);
        }

        // 本測定へ冷間初期化を混入させない。初回呼出は時間外で行い、その後のJITの昇格が
        // 落ち着くまでのウォームアップも結果へ採用しない(本測定のLoopCountはタスク側の確定値を使う)。
        {
            var ウォームアップメソッド = ベンチマークメソッドを解決(ベンチマーク型,タスク.メソッド名);
            var ウォームアップインスタンス = ウォームアップメソッド.IsStatic ? null : Activator.CreateInstance(ベンチマーク型);
            using var ウォームアップ破棄 = ウォームアップインスタンス as IDisposable;
            var ウォームアップ呼出= 引数値一覧.Length==0
                ? ウォームアップメソッド.IsStatic
                    ? (Action)ウォームアップメソッド.CreateDelegate(typeof(Action))
                    : (Action)ウォームアップメソッド.CreateDelegate(typeof(Action),ウォームアップインスタンス)
                : () => ウォームアップメソッド.Invoke(ウォームアップインスタンス,引数値一覧);
            ウォームアップ呼出();
            var JIT待機 = ベンチマークワーカーランタイム.ウォームアップを実行(
                ウォームアップ呼出,
                ベンチマークワーカーランタイム.ウォームアップ目標ナノ秒を正規化(
                    タスク.ウォームアップ目標ナノ秒));
            Console.WriteLine(ベンチマークワーカーランタイム.JIT待機ログを整形(JIT待機));
        }

        var 設定 = ManualConfig
            .Create(ベンチマークワーカーランタイム.設定を作成(
                タスク.成果物フォルダー,
                ループ回数,
                タスク.ベンチマーク反復回数))
            .AddFilter(new ワーカータスクフィルター(タスク));
        var 概要一覧 = BenchmarkSwitcher
            .FromTypes([ベンチマーク型])
            .Run(["--filter","*."+タスク.メソッド名+"*"],設定);
        var 概要 = 概要一覧.SingleOrDefault()
            ??throw new InvalidOperationException($"BenchmarkDotNetから概要が返されませんでした: {タスク.型完全名}.{タスク.メソッド名}");
        var 報告一覧 = 概要.Reports
            .Where(報告 => ワーカータスクフィルター.引数に一致するか(報告.BenchmarkCase,タスク.引数))
            .ToArray();
        if(報告一覧.Length!=1) {
            throw new InvalidOperationException($"BenchmarkDotNetから{タスク.型完全名}.{タスク.メソッド名}の引数「{タスク.引数キー}」に対して{報告一覧.Length}件のレポートが返されました。");
        }
        var 報告 = 報告一覧[0];
        var 統計 = 報告?.ResultStatistics;
        var 割当計測 = 割当計測を取得(報告);
        var ログ = ログ書込.ToString();
        File.WriteAllText(ログパス,ログ,Encoding.UTF8);
        if(!ベンチマークワーカーランタイム.使用可能な統計か(統計?.Mean)) {
            return new ワーカー結果メッセージ(
                タスク.タスクID,
                "Failed",
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                1,
                タスク.成果物フォルダー,
                string.IsNullOrWhiteSpace(ログ)
                    ? "BenchmarkDotNetが結果統計を生成しませんでした。"
                    : ログ,
                ランダムシード,
                結果ダイジェスト);
        }

        // 本測定の戻り値はループ合計で揃える契約。下流の
        // ベンチマーク結果項目が ÷ループ回数 して per-op へ正規化する。
        // 時間統計は BDN が InvocationCount(=ループ回数) で割った per-op 値なので、ここで掛け戻す。
        // GcStatsの総割当量を実際のTotalOperationsで一操作へ直し、LoopCountを掛けて
        // 一バッチへ正規化する。IterationCountはMemoryDiagnoserの総操作数を増やすとは限らない。
        var 合計平均 = 統計?.Mean*ループ回数;
        var 合計誤差 = 統計?.StandardError*ループ回数;
        var 合計標準偏差 = 統計?.StandardDeviation*ループ回数;
        var 合計割当バイト数 = ベンチマークワーカーランタイム.一バッチ割当バイト数を正規化(
            割当計測.総割当バイト数,
            割当計測.総操作回数,
            ループ回数);
        return new ワーカー結果メッセージ(
            タスク.タスクID,
            "Completed",
            ナノ秒を整形(合計平均),
            ナノ秒を整形(合計誤差),
            ナノ秒を整形(合計標準偏差),
            合計平均,
            合計誤差,
            合計標準偏差,
            合計割当バイト数,
            0,
            タスク.成果物フォルダー,
            ログ,
            ランダムシード,
            結果ダイジェスト,
            アフィニティ適用成功か: アフィニティ適用成功か);
    } finally {
        Environment.SetEnvironmentVariable(要素倍率環境変数名,前回要素倍率);
        Environment.SetEnvironmentVariable(要素数環境変数名,前回要素数);
        Environment.SetEnvironmentVariable(ループ回数環境変数名,前回ループ回数);
        Environment.SetEnvironmentVariable(準備済み要素数環境変数名,前回準備済み要素数);
        Environment.SetEnvironmentVariable(ランダムソースシード環境変数名,前回ランダムソースシード);
        Console.SetOut(プロトコル出力);
        Console.SetError(プロトコルエラー);
    }
}

static 演算子対比較結果 演算子対比較を実行(ワーカータスクメッセージ タスク,int 論理CPU) {
    var pair = タスク.Pair??throw new InvalidOperationException("paired-fast-run requires Pair.");
    if(!string.IsNullOrWhiteSpace(タスク.成果物フォルダー)) {
        Directory.CreateDirectory(タスク.成果物フォルダー);
    }
    スレッドアフィニティを適用(論理CPU);
    var fLinqType = ベンチマーク型を解決(pair.FLinq.AssemblyName,pair.FLinq.TypeName);
    var baselineType = ベンチマーク型を解決(pair.Baseline.AssemblyName,pair.Baseline.TypeName);
    using var fLinqElementCount = 要素数スコープを開始(fLinqType.Assembly,pair.ElementCount,pair.ElementScale);
    using var baselineElementCount = 要素数スコープを開始(baselineType.Assembly,pair.ElementCount,pair.ElementScale);
    using var fLinqRandom = ランダムソースシードスコープを開始(fLinqType.Assembly,pair.RandomSeed);
    using var baselineRandom = ランダムソースシードスコープを開始(baselineType.Assembly,pair.RandomSeed);
    var fLinqAction = 演算子呼出を作成(fLinqType,pair.FLinq,out var fLinqInstance);
    var baselineAction = 演算子呼出を作成(baselineType,pair.Baseline,out var baselineInstance);
    using var fLinqDisposable = fLinqInstance as IDisposable;
    using var baselineDisposable = baselineInstance as IDisposable;

    for(var index = 0;index<16;index++) {
        fLinqAction();
        baselineAction();
    }

    var calibration = Math.Max(演算子試行を実行(fLinqAction,1).ナノ秒,演算子試行を実行(baselineAction,1).ナノ秒);
    var loopCount = (int)Math.Clamp(10_000_000d/Math.Max(1d,calibration),1d,100_000d);
    var fLinqSamples = new List<演算子試行結果>(9);
    var baselineSamples = new List<演算子試行結果>(9);
    for(var index = 0;index<9;index++) {
        if((index&1)==0) {
            fLinqSamples.Add(演算子試行を実行(fLinqAction,loopCount));
            baselineSamples.Add(演算子試行を実行(baselineAction,loopCount));
        } else {
            baselineSamples.Add(演算子試行を実行(baselineAction,loopCount));
            fLinqSamples.Add(演算子試行を実行(fLinqAction,loopCount));
        }
    }

    return new 演算子対比較結果("paired-result",loopCount,演算子計測を要約(fLinqSamples,loopCount),演算子計測を要約(baselineSamples,loopCount));
}

static Action 演算子呼出を作成(Type ベンチマーク型,演算子ベンチマーク要求 要求,out object? インスタンス) {
    var 引数 = 要求.Arguments.Select(演算子引数を解析).ToArray();
    var メソッド = ベンチマークメソッドを解決(ベンチマーク型,要求.MethodName);
    インスタンス=メソッド.IsStatic ? null : Activator.CreateInstance(ベンチマーク型);
    if(引数.Length==0) {
        return メソッド.IsStatic
            ? (Action)メソッド.CreateDelegate(typeof(Action))
            : (Action)メソッド.CreateDelegate(typeof(Action),インスタンス);
    }
    var 呼出対象 = インスタンス;
    return () => メソッド.Invoke(呼出対象,引数);
}

static object 演算子引数を解析(string 値) {
    if(long.TryParse(値,NumberStyles.Integer,CultureInfo.InvariantCulture,out var 整数値)) {
        return 整数値;
    }
    if(double.TryParse(値,NumberStyles.Float,CultureInfo.InvariantCulture,out var 浮動小数値)) {
        return 浮動小数値;
    }
    return 値;
}

static 演算子試行結果 演算子試行を実行(Action 呼出,int ループ回数) {
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var gc0Before = GC.CollectionCount(0);
    var gc1Before = GC.CollectionCount(1);
    var gc2Before = GC.CollectionCount(2);
    var stopwatch = Stopwatch.StartNew();
    for(var index = 0;index<ループ回数;index++) {
        呼出();
    }
    stopwatch.Stop();
    return new 演算子試行結果(
        stopwatch.Elapsed.TotalMilliseconds*1_000_000d,
        Math.Max(0,GC.GetAllocatedBytesForCurrentThread()-allocatedBefore),
        GC.CollectionCount(0)-gc0Before,
        GC.CollectionCount(1)-gc1Before,
        GC.CollectionCount(2)-gc2Before);
}

static 演算子計測結果 演算子計測を要約(IReadOnlyList<演算子試行結果> samples,int ループ回数) {
    var orderedNanoseconds = samples.Select(sample => sample.ナノ秒/ループ回数).Order().ToArray();
    var orderedAllocations = samples.Select(sample => sample.割当バイト数/(double)ループ回数).Order().ToArray();
    return new 演算子計測結果(
        orderedNanoseconds[orderedNanoseconds.Length/2],
        orderedAllocations[orderedAllocations.Length/2],
        samples.Sum(static sample => sample.Gen0),
        samples.Sum(static sample => sample.Gen1),
        samples.Sum(static sample => sample.Gen2),
        samples.Select(sample=>sample.ナノ秒/ループ回数).ToArray(),
        samples.Select(sample=>checked((long)Math.Round(sample.割当バイト数/(double)ループ回数,MidpointRounding.AwayFromZero))).ToArray(),
        samples.Select(static sample=>sample.Gen0).ToArray(),
        samples.Select(static sample=>sample.Gen1).ToArray(),
        samples.Select(static sample=>sample.Gen2).ToArray());
}
static ワーカー結果メッセージ 高速ベンチマークを実行(
    ワーカータスクメッセージ タスク,
    int 論理CPU,
    TextWriter プロトコル出力,
    TextWriter プロトコルエラー) {
    Directory.CreateDirectory(タスク.成果物フォルダー);
    var ログパス = Path.Combine(タスク.成果物フォルダー,"fast-worker.log");
    object? インスタンス = null;
    try {
        var アフィニティ適用成功か = スレッドアフィニティを適用(論理CPU);
        var ベンチマーク型 = ベンチマーク型を解決(タスク.アセンブリ名,タスク.型完全名);
        var メソッド = ベンチマークメソッドを解決(ベンチマーク型,タスク.メソッド名);
        var ランダムシード = ランダムソースシードを作成(タスク);
        var ループ回数 = Math.Max(1,タスク.ループ回数);
        using var 要素数スコープ = 要素数スコープを開始(ベンチマーク型.Assembly,タスク.要素数,タスク.要素倍率);
        using var ランダムソーススコープ = ランダムソースシードスコープを開始(ベンチマーク型.Assembly,ランダムシード);
        インスタンス=メソッド.IsStatic ? null : Activator.CreateInstance(ベンチマーク型);
        var 引数値一覧 = ベンチマーク引数フォーマッター.オブジェクト一覧に変換(タスク.引数);
        var 結果ダイジェスト = ベンチマーク結果ダイジェストを取得(ベンチマーク型,タスク.メソッド名,引数値一覧);
        var ベンチマーク呼出 = 引数値一覧.Length==0
            ? メソッド.IsStatic
                ? (Action)メソッド.CreateDelegate(typeof(Action))
                : (Action)メソッド.CreateDelegate(typeof(Action),インスタンス)
            : () => メソッド.Invoke(インスタンス,引数値一覧);
        if(タスク.開放ループ較正か) {
            var 較正閾値ナノ秒 = タスク.較正閾値ナノ秒
                ??throw new InvalidOperationException("開放ループ較正には較正閾値ナノ秒が必要です。");
            var 較正結果 = ベンチマークワーカーランタイム.ループ回数を較正(ベンチマーク呼出,較正閾値ナノ秒,ループ回数);
            File.WriteAllText(
                ログパス,
                "高速ワーカーの較正プローブが完了しました。CPU="
                +(論理CPU>=0 ? 論理CPU.ToString(CultureInfo.InvariantCulture) : "-")
                +"、"+較正ログを整形(較正結果),
                Encoding.UTF8);
            return new ワーカー結果メッセージ(
                タスク.タスクID,
                "Completed",
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                0,
                タスク.成果物フォルダー,
                string.Empty,
                ランダムシード,
                結果ダイジェスト,
                実測ループ回数: 較正結果.ループ回数,
                アフィニティ適用成功か: アフィニティ適用成功か);
        }

        ベンチマーク呼出();
        var JIT待機 = ベンチマークワーカーランタイム.ウォームアップを実行(
            ベンチマーク呼出,
            ベンチマークワーカーランタイム.ウォームアップ目標ナノ秒を正規化(
                タスク.ウォームアップ目標ナノ秒));
        var 計測 = 計測を実行(ベンチマーク呼出,ループ回数);
        var 平均ナノ秒 = 計測.中央値ナノ秒;
        var 割当バイト数 = 計測.割り当てバイト数;
        var カウンター = new global::Seamless.Linq.LinqCounter();
        using(var カウンタースコープ = global::Seamless.Linq.LinqCounterScope.Start(カウンター)) {
            ベンチマーク呼出();
        }
        var 診断カウンター = 診断カウンターを作成(カウンター);
        File.WriteAllText(
            ログパス,
            "高速ワーカーの直接ベンチマークが完了しました。CPU="
            +(論理CPU>=0 ? 論理CPU.ToString(CultureInfo.InvariantCulture) : "-")
            +"、"+ベンチマークワーカーランタイム.JIT待機ログを整形(JIT待機)
            +"、"+計測ログを整形(計測)
            +"、割り当てバイト数="+割当バイト数.ToString(CultureInfo.InvariantCulture),
            Encoding.UTF8);
        return new ワーカー結果メッセージ(
            タスク.タスクID,
            "Completed",
            ナノ秒を整形(平均ナノ秒),
            ナノ秒を整形(計測.誤差ナノ秒),
            ナノ秒を整形(計測.標準偏差ナノ秒),
            平均ナノ秒,
            計測.誤差ナノ秒,
            計測.標準偏差ナノ秒,
            割当バイト数,
            0,
            タスク.成果物フォルダー,
            string.Empty,
            ランダムシード,
            結果ダイジェスト,
            診断カウンター,
            アフィニティ適用成功か: アフィニティ適用成功か,
            サンプル一覧: 計測.サンプル一覧,
            GC収集回数一覧: 計測.GC収集回数一覧);
    } catch(Exception 例外) when(例外 is not OperationCanceledException) {
        var 実例外 = 例外 is TargetInvocationException { InnerException: { } 内部例外 } ? 内部例外 : 例外;
        Directory.CreateDirectory(タスク.成果物フォルダー);
        File.WriteAllText(ログパス,実例外.ToString(),Encoding.UTF8);
        return new ワーカー結果メッセージ(
            タスク.タスクID,
            "Failed",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            1,
            タスク.成果物フォルダー,
            実例外.ToString(),
            null,
            string.Empty);
    } finally {
        if(インスタンス is IDisposable 破棄可能) {
            破棄可能.Dispose();
        }
        Console.SetOut(プロトコル出力);
        Console.SetError(プロトコルエラー);
    }
}

/// <summary>破棄2サンプルと実測7サンプルを同一LoopCountで取得する直接測定。</summary>
static 計測結果 計測を実行(Action ベンチマーク呼出,int 指定ループ回数) {
    var 時計 = new Stopwatch();
    var ダミー累積 = 0d;
    var ループ回数 = Math.Max(1,指定ループ回数);
    var 全サンプル数 = 計測設定.破棄サンプル数+計測設定.計測サンプル数;

    var サンプル一覧 = new double[計測設定.計測サンプル数];
    long? 割り当てバイト数 = null;
    var __gcCollections = new int[3];
    for(var サンプル番号 = 0;サンプル番号<全サンプル数;サンプル番号++) {
        // 先頭の 破棄サンプル数 本は時間ベースのウォームアップとして捨てる。左右とも同じ本数・同じ回数を回す。
        var 計測対象か = サンプル番号>=計測設定.破棄サンプル数;
        var 割当前バイト数 = GC.GetAllocatedBytesForCurrentThread();
        var __gc0Before=GC.CollectionCount(0);
        var __gc1Before=GC.CollectionCount(1);
        var __gc2Before=GC.CollectionCount(2);
        時計.Restart();
        バッチを実行(ベンチマーク呼出,ループ回数);
        時計.Stop();
        ダミー累積+=時計.Elapsed.TotalMilliseconds;
        if(!計測対象か) {
            continue;
        }
        __gcCollections[0]+=GC.CollectionCount(0)-__gc0Before;
        __gcCollections[1]+=GC.CollectionCount(1)-__gc1Before;
        __gcCollections[2]+=GC.CollectionCount(2)-__gc2Before;
        // 割り当てバイト数は「1 サンプル＝ループ回数 反復ぶん」で、時間の単位と揃える。
        割り当てバイト数??=Math.Max(0,GC.GetAllocatedBytesForCurrentThread()-割当前バイト数);
        サンプル一覧[サンプル番号-計測設定.破棄サンプル数]=時計.Elapsed.TotalMilliseconds*1_000_000d;
    }

    var 並び替え済み = サンプル一覧.OrderBy(static 値 => 値).ToArray();
    var 中央値ナノ秒 = 並び替え済み[並び替え済み.Length/2];
    var 平均ナノ秒 = サンプル一覧.Average();
    var 分散 = サンプル一覧.Length<2
        ? 0d
        : サンプル一覧.Sum(値 => (値-平均ナノ秒)*(値-平均ナノ秒))/(サンプル一覧.Length-1);
    var 標準偏差ナノ秒 = Math.Sqrt(分散);
    return new 計測結果(
        中央値ナノ秒,
        標準偏差ナノ秒/Math.Sqrt(サンプル一覧.Length),
        標準偏差ナノ秒,
        ループ回数,
        割り当てバイト数??0,
        ダミー累積,
        サンプル一覧,
        __gcCollections);
}

/// <summary>Stopwatch読取を含まない、1バッチ分の反復。</summary>
static void バッチを実行(Action ベンチマーク呼出,int ループ回数) {
    for(var 反復番号 = 0;反復番号<ループ回数;反復番号++) {
        ベンチマーク呼出();
    }
}

static string 較正ログを整形(較正結果 較正) {
    var ログ = "ループ回数="+較正.ループ回数.ToString(CultureInfo.InvariantCulture)
        +"、バッチ時間="+ナノ秒を整形(較正.バッチナノ秒)
        +"、ウォームアップ時間="+ナノ秒を整形(較正.ウォームアップ目標ナノ秒);
    if(較正.JIT待機 is { } JIT待機) {
        ログ+="、"+ベンチマークワーカーランタイム.JIT待機ログを整形(JIT待機);
    }

    return ログ;
}

static string 計測ログを整形(計測結果 計測) {
    var サンプル = string.Join(", ",計測.サンプル一覧.Select(static 値 => 値.ToString("F0",CultureInfo.InvariantCulture)));
    return "ループ回数="+計測.ループ回数.ToString(CultureInfo.InvariantCulture)
        +"、破棄サンプル数="+計測設定.破棄サンプル数.ToString(CultureInfo.InvariantCulture)
        +"、計測サンプル数="+計測.サンプル一覧.Count.ToString(CultureInfo.InvariantCulture)
        +"、中央値="+ナノ秒を整形(計測.中央値ナノ秒)
        +"、標準偏差="+ナノ秒を整形(計測.標準偏差ナノ秒)
        +"、サンプル(ns)=["+サンプル+"]"
        +"、GC(Gen0/Gen1/Gen2)="+string.Join("/",計測.GC収集回数一覧.Select(static 回数 => 回数.ToString(CultureInfo.InvariantCulture)))
        +"、ダミー累積="+計測.ダミー累積.ToString("F3",CultureInfo.InvariantCulture);
}

static Type ベンチマーク型を解決(string アセンブリ名,string 型完全名) {
    var アセンブリ = AppDomain.CurrentDomain
        .GetAssemblies()
        .FirstOrDefault(項目 => string.Equals(項目.GetName().Name,アセンブリ名,StringComparison.OrdinalIgnoreCase))
        ??Assembly.Load(new AssemblyName(アセンブリ名));
    return アセンブリ.GetType(型完全名,throwOnError: false,ignoreCase: false)
        ??throw new TypeLoadException($"ベンチマーク型が見つかりませんでした: {アセンブリ名}:{型完全名}");
}

static MethodInfo ベンチマークメソッドを解決(Type ベンチマーク型,string メソッド名) {
    var メソッド一覧 = ベンチマーク型.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static);
    var メソッド = メソッド一覧.SingleOrDefault(候補 =>
        string.Equals(候補.Name,メソッド名,StringComparison.Ordinal)
        &&ベンチマーク属性を持つか(候補));
    return メソッド??throw new MissingMethodException(ベンチマーク型.FullName,メソッド名);
}

static bool ベンチマーク属性を持つか(MethodInfo メソッド) =>
    メソッド.GetCustomAttributes(inherit: true).Any(static 属性 =>
        string.Equals(属性.GetType().Name,"BenchmarkAttribute",StringComparison.Ordinal));

static IDisposable 要素数スコープを開始(Assembly アセンブリ,int 要素数,string 要素倍率) {
    var 実行モード型 = アセンブリ.GetType("Common.BenchmarkExecutionMode",throwOnError: false,ignoreCase: false)
        ??AppDomain.CurrentDomain.GetAssemblies()
            .Select(static 候補 => 候補.GetType("Common.BenchmarkExecutionMode",throwOnError: false,ignoreCase: false))
            .FirstOrDefault(static 候補 => 候補 is not null);
    var メソッド = 実行モード型?.GetMethod(
        "UseElementCount",
        BindingFlags.Public|BindingFlags.Static,
        binder: null,
        types: [typeof(int),typeof(string)],
        modifiers: null);
    return メソッド?.Invoke(null,[要素数,string.IsNullOrWhiteSpace(要素倍率) ? "1" : 要素倍率]) as IDisposable
        ??空ディスポーザブル.インスタンス;
}

static IDisposable ランダムソースシードスコープを開始(Assembly アセンブリ,int シード) {
    var 実行モード型 = アセンブリ.GetType("Common.BenchmarkExecutionMode",throwOnError: false,ignoreCase: false);
    var メソッド = 実行モード型?.GetMethod(
        "UseRandomSourceSeed",
        BindingFlags.Public|BindingFlags.Static,
        binder: null,
        types: [typeof(int)],
        modifiers: null);
    return メソッド?.Invoke(null,[シード]) as IDisposable
        ??空ディスポーザブル.インスタンス;
}

static string ベンチマーク結果ダイジェストを取得(Type ベンチマーク型,string メソッド名,object?[] 引数一覧) {
    var インスタンス = Activator.CreateInstance(ベンチマーク型);
    var メソッド = ベンチマーク型.GetMethod(
        "CaptureBenchmarkMethodResult",
        BindingFlags.Public|BindingFlags.Instance,
        binder: null,
        types: [typeof(string),typeof(bool),typeof(object[])],
        modifiers: null)
        ??throw new MissingMethodException(ベンチマーク型.FullName,"CaptureBenchmarkMethodResult");
    try {
        return メソッド.Invoke(インスタンス,[メソッド名,false,引数一覧]) as string
            ??throw new InvalidOperationException("ベンチマーク結果ダイジェストが空でした。");
    } finally {
        if(インスタンス is IDisposable 破棄対象) {
            破棄対象.Dispose();
        }
    }
}

static int ランダムソースシードを作成(ワーカータスクメッセージ タスク) {
    var 実行ルート = 実行ルートを推定(タスク.成果物フォルダー);
    return 安定ハッシュ(実行ルート+"|"+タスク.ソースタブ+"|"+タスク.メソッド名+"|"+タスク.引数キー+"|"+タスク.要素数.ToString(CultureInfo.InvariantCulture)+"|"+タスク.ループ回数.ToString(CultureInfo.InvariantCulture));
}

static string 実行ルートを推定(string 成果物フォルダー) {
    var ディレクトリ = new DirectoryInfo(成果物フォルダー);
    for(var 階層 = 0;階層<4&&ディレクトリ is not null;階層++) {
        ディレクトリ=ディレクトリ.Parent;
    }
    return ディレクトリ?.FullName??成果物フォルダー;
}

static int 安定ハッシュ(string 文字列) {
    unchecked {
        var ハッシュ = (int)2166136261;
        foreach(var 文字 in 文字列) {
            ハッシュ^=文字;
            ハッシュ*=16777619;
        }
        return ハッシュ==0 ? 1 : ハッシュ;
    }
}

static string ナノ秒を整形(double? 値) {
    return 値 is { } 数値&&double.IsFinite(数値)
        ? 数値.ToString("N3",CultureInfo.InvariantCulture)+" ns"
        : string.Empty;
}

static IReadOnlyList<ベンチマーク診断カウンター> 診断カウンターを作成(global::Seamless.Linq.LinqCounter カウンター) => [
    new("PhysicalMoveNext",カウンター.PhysicalMoveNext),
    new("LogicalOperations",カウンター.LogicalOperations),
    new("LogicalWhere",カウンター.LogicalWhere),
    new("LogicalSelect",カウンター.LogicalSelect),
    new("HashBuild",カウンター.HashBuild),
    new("HashProbe",カウンター.HashProbe),
    new("BufferAdd",カウンター.BufferAdd),
    new("TryGetNonEnumeratedCount",カウンター.TryGetNonEnumeratedCount),
    new("LogicalDelegate",カウンター.LogicalDelegate),
    new("PhysicalDelegate",カウンター.PhysicalDelegate),
    new("EnumerableFallback",カウンター.EnumerableFallback),
    new("DirectSpanUsed",カウンター.DirectSpanUsed),
    new("TryGetSpanCalled",カウンター.TryGetSpanCalled),
    new("TryGetSpanSucceeded",カウンター.TryGetSpanSucceeded),
    new("TryGetSpanFailed",カウンター.TryGetSpanFailed),
    new("EnumerationLoopUsed",カウンター.EnumerationLoopUsed),
    new("FastCopyUsed",カウンター.FastCopyUsed),
    new("HashCapacityHintUsed",カウンター.HashCapacityHintUsed),
];

static BDN割当計測 割当計測を取得(object? 報告) {
    var GC統計 = 報告?.GetType().GetProperty("GcStats")?.GetValue(報告);
    if(GC統計 is null) {
        return new(null,null);
    }

    var 総操作回数 = 非負整数に変換(GC統計.GetType().GetProperty("TotalOperations")?.GetValue(GC統計));
    var 合計取得メソッド = GC統計.GetType().GetMethod("GetTotalAllocatedBytes",new[] { typeof(bool) });
    var クォンタム込合計 = 非負整数に変換(合計取得メソッド?.Invoke(GC統計,new object[] { false }));
    var クォンタム除外合計 = 非負整数に変換(合計取得メソッド?.Invoke(GC統計,new object[] { true }));
    var 割当バイト数 = 非負整数に変換(GC統計.GetType().GetProperty("AllocatedBytes")?.GetValue(GC統計));
    foreach(var 値 in new[] { クォンタム込合計,割当バイト数,クォンタム除外合計 }) {
        if(値 is >0) {
            return new(値,総操作回数);
        }
    }

    var 総割当バイト数 = クォンタム込合計>=0
        ? クォンタム込合計
        : 割当バイト数>=0
            ? 割当バイト数
            : クォンタム除外合計>=0
                ? クォンタム除外合計
                : null;
    return new(総割当バイト数,総操作回数);
}

static long? 非負整数に変換(object? 値) =>
    値 is long 数値&&数値>=0
        ? 数値
        : null;

static string UTF8BOMを除去(string 文字列) {
    return 文字列.Length>0&&文字列[0]=='\uFEFF'
        ? 文字列[1..]
        : 文字列;
}

static int 必須整数引数を読む(string[] 引数一覧,string 名前) {
    var 値 = 必須文字列引数を読む(引数一覧,名前);
    return int.Parse(値,CultureInfo.InvariantCulture);
}

static string 必須文字列引数を読む(string[] 引数一覧,string 名前) {
    for(var 引数番号 = 0;引数番号<引数一覧.Length-1;引数番号++) {
        if(string.Equals(引数一覧[引数番号],名前,StringComparison.OrdinalIgnoreCase)) {
            return 引数一覧[引数番号+1];
        }
    }

    throw new ArgumentException($"必須引数がありません: {名前}");
}

static bool アフィニティを適用(int 論理CPU) {
    if(論理CPU==-1) {
        return false;
    }

    var マスク = ベンチマークワーカーランタイム.アフィニティマスクを作成(
        論理CPU,
        Environment.ProcessorCount,
        IntPtr.Size*8);
    if(OperatingSystem.IsWindows()) {
        Windowsプロセスアフィニティマスクを適用(マスク);
        スレッドアフィニティマスクを適用(論理CPU,マスク);
        return true;
    }
    if(OperatingSystem.IsLinux()) {
        Linuxプロセスアフィニティマスクを適用(マスク);
        return true;
    }

    throw new PlatformNotSupportedException();
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static void Windowsプロセスアフィニティマスクを適用(ulong マスク) => プロセスアフィニティマスクを適用(マスク);

[System.Runtime.Versioning.SupportedOSPlatform("linux")]
static void Linuxプロセスアフィニティマスクを適用(ulong マスク) => プロセスアフィニティマスクを適用(マスク);

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
static void プロセスアフィニティマスクを適用(ulong マスク) {
    using var プロセス = Process.GetCurrentProcess();
    プロセス.ProcessorAffinity=new IntPtr(checked((long)マスク));
    if(unchecked((ulong)プロセス.ProcessorAffinity.ToInt64())!=マスク) {
        throw new InvalidOperationException(nameof(Process.ProcessorAffinity));
    }
}

static bool スレッドアフィニティを適用(int 論理CPU) {
    if(論理CPU==-1) {
        return false;
    }
    if(!OperatingSystem.IsWindows()) {
        throw new PlatformNotSupportedException();
    }

    var マスク = ベンチマークワーカーランタイム.アフィニティマスクを作成(
        論理CPU,
        Environment.ProcessorCount,
        IntPtr.Size*8);
    スレッドアフィニティマスクを適用(論理CPU,マスク);
    return true;
}

static void スレッドアフィニティマスクを適用(int 論理CPU,ulong マスク) {
    var 以前のマスク = SetThreadAffinityMask(GetCurrentThread(),new UIntPtr(マスク));
    ベンチマークワーカーランタイム.スレッドアフィニティ適用結果を検証(
        以前のマスク.ToUInt64(),
        論理CPU,
        Marshal.GetLastPInvokeError());
}

[DllImport("kernel32.dll")]
static extern IntPtr GetCurrentThread();

[DllImport("kernel32.dll",SetLastError = true)]
static extern UIntPtr SetThreadAffinityMask(IntPtr スレッドハンドル,UIntPtr スレッドアフィニティマスク);

internal sealed record ワーカータスクメッセージ(
    string コマンド,
    string タスクID,
    string アセンブリ名,
    string 型完全名,
    string メソッド名,
    string 引数キー,
    ベンチマーク引数[] 引数,
    int ソースタブ,
    int[] 準備済み要素数,
    int 要素数,
    int ループ回数,
    string 要素倍率,
    string 成果物フォルダー,
    int? 割り当て論理CPU = null,
    bool 開放ループ較正か = false,
    double? 較正閾値ナノ秒 = null,
    int ベンチマーク反復回数 = 1,
    double ウォームアップ目標ナノ秒 = 300_000_000d) {
    public string? Command { get; init; }
    public 演算子対比較要求? Pair { get; init; }
}

internal sealed record 演算子対比較要求(
    演算子ベンチマーク要求 FLinq,
    演算子ベンチマーク要求 Baseline,
    int ElementCount,
    string ElementScale,
    int RandomSeed);

internal sealed record 演算子ベンチマーク要求(
    string AssemblyName,
    string TypeName,
    string MethodName,
    string[] Arguments);

internal sealed record 演算子試行結果(
    double ナノ秒,
    long 割当バイト数,
    int Gen0,
    int Gen1,
    int Gen2);

internal sealed record 演算子計測結果(
    double 中央値ナノ秒,
    double 割当バイト数,
    int Gen0,
    int Gen1,
    int Gen2,
    double[] ナノ秒サンプル,
    long[] 割当バイトサンプル,
    int[] Gen0サンプル,
    int[] Gen1サンプル,
    int[] Gen2サンプル);

internal sealed record 演算子対比較結果(
    string Kind,
    int LoopCount,
    演算子計測結果 FLinq,
    演算子計測結果 Baseline);
internal sealed record ベンチマーク引数(string 名前,string 値);

/// <summary>
/// 計測サンプルの本数。左右で必ず同じ値を使うため定数にしている。
/// 計測サンプル数は中央値が一意に決まるよう奇数にする。
/// </summary>
internal static class 計測設定 {
    public const int 破棄サンプル数 = 2;
    public const int 計測サンプル数 = 7;
}

internal sealed record BDN割当計測(long? 総割当バイト数,long? 総操作回数);

/// <summary>1 サンプル（＝ループ回数 反復）を単位とした計測結果。時間はすべてループ合計ナノ秒。</summary>
internal sealed record 計測結果(
    double 中央値ナノ秒,
    double 誤差ナノ秒,
    double 標準偏差ナノ秒,
    int ループ回数,
    long 割り当てバイト数,
    double ダミー累積,
    IReadOnlyList<double> サンプル一覧,
    IReadOnlyList<int> GC収集回数一覧);

internal static class ベンチマーク引数フォーマッター {
    public static object?[] オブジェクト一覧に変換(IReadOnlyList<ベンチマーク引数> 引数一覧) =>
        引数一覧.Select(static 引数 => 値を解析(引数.値)).ToArray();

    private static object 値を解析(string 値) {
        if(long.TryParse(値,NumberStyles.Integer,CultureInfo.InvariantCulture,out var 整数値)) {
            return 整数値;
        }
        if(double.TryParse(値,NumberStyles.Float,CultureInfo.InvariantCulture,out var 浮動小数値)) {
            return 浮動小数値;
        }
        return 値;
    }
}

internal sealed record ベンチマーク診断カウンター(
    string カウンター名,
    double カウンター値);

internal sealed record ワーカー結果メッセージ(
    string タスクID,
    string 状態,
    string 平均,
    string 誤差,
    string 標準偏差,
    double? 平均ナノ秒,
    double? 誤差ナノ秒,
    double? 標準偏差ナノ秒,
    long? 割り当てバイト数,
    int 終了コード,
    string 成果物フォルダー,
    string ログ,
    int? ランダムシード,
    string 結果ダイジェスト,
    IReadOnlyList<ベンチマーク診断カウンター>? 診断カウンター = null,
    int? 実測ループ回数 = null,
    bool アフィニティ適用成功か = false,
    IReadOnlyList<double>? サンプル一覧 = null,
    IReadOnlyList<int>? GC収集回数一覧 = null);
internal sealed class 空ディスポーザブル:IDisposable {
    public static 空ディスポーザブル インスタンス { get; } = new();

    public void Dispose() {
    }
}

internal sealed class ワーカータスクフィルター(ワーカータスクメッセージ タスク):IFilter {
    public bool Predicate(BenchmarkDotNet.Running.BenchmarkCase ベンチマークケース) =>
        ベンチマークケース.Descriptor.WorkloadMethod.Name==タスク.メソッド名
        &&引数に一致するか(ベンチマークケース,タスク.引数);

    /// <summary>
    /// ベンチマークケースの実引数がタスクの引数と一致するかを判定する。
    /// DisplayInfo の部分文字列一致にすると、Index=64 が Memory=67108864 の末尾に一致してしまうような
    /// 誤検出が起きるため、引数名と整形済み値の完全一致で照合する。
    /// BDN の実行対象を絞るフィルターと、結果レポートの選択の両方が同じ判定を使う。
    /// </summary>
    public static bool 引数に一致するか(BenchmarkDotNet.Running.BenchmarkCase ベンチマークケース,ベンチマーク引数[] 引数一覧) {
        if(引数一覧.Length==0) {
            return true;
        }
        var 実引数一覧 = ベンチマークケース.Parameters.Items;
        return 引数一覧.All(引数 => 実引数一覧.Any(実引数 =>
            string.Equals(実引数.Name,引数.名前,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(引数値を整形(実引数.Value),引数.値,StringComparison.Ordinal)));
    }

    /// <summary>UI 側の ベンチマークカタログ.引数値を整形 と同じ規則で引数値を文字列化する。</summary>
    private static string 引数値を整形(object? 値) =>
        値 is IFormattable 整形可能値
            ? 整形可能値.ToString(null,CultureInfo.InvariantCulture)
            : 値?.ToString()??string.Empty;
}
