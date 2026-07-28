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

        if(string.Equals(タスク.コマンド,"shutdown",StringComparison.OrdinalIgnoreCase)) {
            await Task.WhenAll(高速タスク一覧.ToArray());
            return 0;
        }

        if(string.Equals(タスク.コマンド,"fast-run",StringComparison.OrdinalIgnoreCase)) {
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
        アフィニティを適用(論理CPU);
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
        if(タスク.開放ループ較正か&&タスク.較正閾値ナノ秒 is { } 較正閾値ナノ秒) {
            // 較正ケースはBenchmarkDotNetを介さず、開放ループの実測値をそのまま結果として返す(高速プロファイルと同じ計測方式)。
            // ウォームアップ・統計処理・外れ値除去の厳密さは失うが、固定コスト(Reflection.Emitラップ・エンジン起動)を1回分節約できる。
            var プローブメソッド = ベンチマークメソッドを解決(ベンチマーク型,タスク.メソッド名);
            var プローブインスタンス = プローブメソッド.IsStatic ? null : Activator.CreateInstance(ベンチマーク型);
            var プローブ呼出 = 引数値一覧.Length==0
                ? プローブメソッド.IsStatic
                    ? (Action)プローブメソッド.CreateDelegate(typeof(Action))
                    : (Action)プローブメソッド.CreateDelegate(typeof(Action),プローブインスタンス)
                : () => プローブメソッド.Invoke(プローブインスタンス,引数値一覧);
            var プローブ計測 = 計測を実行(プローブ呼出,true,較正閾値ナノ秒,ループ回数);
            var 実測ループ回数 = プローブ計測.ループ回数;
            Environment.SetEnvironmentVariable(ループ回数環境変数名,実測ループ回数.ToString(CultureInfo.InvariantCulture));
            var プローブ平均ナノ秒 = プローブ計測.中央値ナノ秒;
            Console.WriteLine("較正計測: "+計測ログを整形(プローブ計測));
            var プローブログ = ログ書込.ToString();
            File.WriteAllText(ログパス,プローブログ,Encoding.UTF8);
            return new ワーカー結果メッセージ(
                タスク.タスクID,
                "Completed",
                ナノ秒を整形(プローブ平均ナノ秒),
                ナノ秒を整形(プローブ計測.誤差ナノ秒),
                ナノ秒を整形(プローブ計測.標準偏差ナノ秒),
                プローブ平均ナノ秒,
                プローブ計測.誤差ナノ秒,
                プローブ計測.標準偏差ナノ秒,
                プローブ計測.割り当てバイト数,
                0,
                タスク.成果物フォルダー,
                プローブログ,
                ランダムシード,
                結果ダイジェスト,
                実測ループ回数: 実測ループ回数);
        }

        var 設定 = ManualConfig
            .Create(ベンチマークワーカーランタイム.設定を作成(タスク.成果物フォルダー,ループ回数))
            .AddFilter(new ワーカータスクフィルター(タスク));
        var 概要一覧 = BenchmarkSwitcher
            .FromTypes([ベンチマーク型])
            .Run(["--filter","*."+タスク.メソッド名+"*"],設定);
        var 概要 = 概要一覧.SingleOrDefault()
            ??throw new InvalidOperationException($"BenchmarkDotNetから概要が返されませんでした: {タスク.型完全名}.{タスク.メソッド名}");
        var 報告一覧 = 概要.Reports
            .Where(報告 => 引数に一致する報告か(報告.BenchmarkCase,タスク.引数))
            .ToArray();
        if(報告一覧.Length!=1) {
            throw new InvalidOperationException($"BenchmarkDotNetから{タスク.型完全名}.{タスク.メソッド名}の引数「{タスク.引数キー}」に対して{報告一覧.Length}件のレポートが返されました。");
        }
        var 報告 = 報告一覧[0];
        var 統計 = 報告?.ResultStatistics;
        var 割当バイト数 = 割当バイト数を取得(報告);
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

        // ワーカーの戻り値はループ合計で揃える契約（開放ループ較正の経路も合計を返す）。下流の
        // ベンチマーク結果項目が ÷ループ回数 して per-op へ正規化する。
        // 時間統計は BDN が InvocationCount(=ループ回数) で割った per-op 値なので、ここで掛け戻す。
        // 割当バイト数は GcStats.GetTotalOperations が 1 反復×ループ回数 のため既にループ合計であり、
        // 掛けてはならない（掛けると二重計上になり、UI のメモリ列だけ N 倍で表示される）。
        var 合計平均 = 統計?.Mean*ループ回数;
        var 合計誤差 = 統計?.StandardError*ループ回数;
        var 合計標準偏差 = 統計?.StandardDeviation*ループ回数;
        long? 合計割当バイト数 = 割当バイト数;
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
            結果ダイジェスト);
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

static ワーカー結果メッセージ 高速ベンチマークを実行(
    ワーカータスクメッセージ タスク,
    int 論理CPU,
    TextWriter プロトコル出力,
    TextWriter プロトコルエラー) {
    Directory.CreateDirectory(タスク.成果物フォルダー);
    var ログパス = Path.Combine(タスク.成果物フォルダー,"fast-worker.log");
    object? インスタンス = null;
    try {
        スレッドアフィニティを適用(論理CPU);
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
        var 計測 = 計測を実行(ベンチマーク呼出,タスク.開放ループ較正か,タスク.較正閾値ナノ秒,ループ回数);
        int? 実測ループ回数 = タスク.開放ループ較正か ? 計測.ループ回数 : null;
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
            実測ループ回数);
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

/// <summary>
/// 左右（最適化有効側／無効側）で完全に同じ形の計測を行う共通ルーチン。
/// <para>
/// 較正は「1 サンプルあたりのループ回数」を決めるためだけに使い、その時間は結果に採らない。
/// 続くサンプリングは左右とも同じループ回数・同じサンプル数・同じコードパスで回す。
/// </para>
/// <para>
/// 内側ループは較正・計測とも 1 反復につき 1 回だけ <see cref="Stopwatch.Elapsed"/> を読む。
/// 較正側は終了判定に使い、計測側はダミーとして累積するだけだが、こうしないと
/// 左右で 1 反復あたりの時計読みコストが食い違い、較正側（＝右列）だけが系統的に遅く見える。
/// </para>
/// </summary>
static 計測結果 計測を実行(Action ベンチマーク呼出,bool 開放ループ較正か,double? 較正閾値ナノ秒,int 指定ループ回数) {
    var 時計 = new Stopwatch();
    var ダミー累積 = 0d;
    var ループ回数 = Math.Max(1,指定ループ回数);
    var 全サンプル数 = 計測設定.破棄サンプル数+計測設定.計測サンプル数;

    if(開放ループ較正か&&較正閾値ナノ秒 is { } 閾値ナノ秒) {
        // 較正 1 回 + 全サンプルで合計がおおよそ閾値に収まるよう、1 サンプルの目標時間を割り当てる。
        var 目標ナノ秒 = Math.Max(1d,閾値ナノ秒/(全サンプル数+1));
        var 反復回数 = 0;
        時計.Restart();
        while(時計.Elapsed.TotalMilliseconds*1_000_000d<目標ナノ秒) {
            ベンチマーク呼出();
            反復回数++;
        }
        時計.Stop();
        ループ回数=Math.Max(1,反復回数);
    } else {
        // 較正しない側は較正パスに相当する空回しを 1 サンプル分だけ行い、事前実行量を揃える。
        時計.Restart();
        ダミー累積+=反復ループを回す(ベンチマーク呼出,ループ回数,時計);
        時計.Stop();
    }

    var サンプル一覧 = new double[計測設定.計測サンプル数];
    long? 割り当てバイト数 = null;
    for(var サンプル番号 = 0;サンプル番号<全サンプル数;サンプル番号++) {
        // 先頭の 破棄サンプル数 本は時間ベースのウォームアップとして捨てる。左右とも同じ本数・同じ回数を回す。
        var 計測対象か = サンプル番号>=計測設定.破棄サンプル数;
        var 割当前バイト数 = GC.GetAllocatedBytesForCurrentThread();
        時計.Restart();
        ダミー累積+=反復ループを回す(ベンチマーク呼出,ループ回数,時計);
        時計.Stop();
        if(!計測対象か) {
            continue;
        }
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
        サンプル一覧);
}

/// <summary>
/// 計測の内側ループ。1 反復につき 1 回だけ時計を読む形を左右・較正／計測で共有するためのもの。
/// 戻り値のダミー累積は JIT に消されないよう呼び出し側でログへ書き出す。
/// </summary>
static double 反復ループを回す(Action ベンチマーク呼出,int ループ回数,Stopwatch 時計) {
    var ダミー累積 = 0d;
    for(var 反復番号 = 0;反復番号<ループ回数;反復番号++) {
        ベンチマーク呼出();
        ダミー累積+=時計.Elapsed.TotalMilliseconds;
    }

    return ダミー累積;
}

static string 計測ログを整形(計測結果 計測) {
    var サンプル = string.Join(", ",計測.サンプル一覧.Select(static 値 => 値.ToString("F0",CultureInfo.InvariantCulture)));
    return "ループ回数="+計測.ループ回数.ToString(CultureInfo.InvariantCulture)
        +"、破棄サンプル数="+計測設定.破棄サンプル数.ToString(CultureInfo.InvariantCulture)
        +"、計測サンプル数="+計測.サンプル一覧.Count.ToString(CultureInfo.InvariantCulture)
        +"、中央値="+ナノ秒を整形(計測.中央値ナノ秒)
        +"、標準偏差="+ナノ秒を整形(計測.標準偏差ナノ秒)
        +"、サンプル(ns)=["+サンプル+"]"
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
    return メソッド.Invoke(インスタンス,[メソッド名,false,引数一覧]) as string
        ??throw new InvalidOperationException("ベンチマーク結果ダイジェストが空でした。");
}

static int ランダムソースシードを作成(ワーカータスクメッセージ タスク) {
    var 実行ルート = 実行ルートを推定(タスク.成果物フォルダー);
    return 安定ハッシュ(実行ルート+"|"+タスク.ソースタブ+"|"+タスク.メソッド名+"|"+タスク.引数キー+"|"+タスク.要素数.ToString(CultureInfo.InvariantCulture)+"|"+タスク.ループ回数.ToString(CultureInfo.InvariantCulture));
}

static bool 引数に一致する報告か(BenchmarkDotNet.Running.BenchmarkCase ベンチマークケース,ベンチマーク引数[] 引数一覧) {
    if(引数一覧.Length==0) {
        return true;
    }
    var 表示 = ベンチマークケース.DisplayInfo;
    return 引数一覧.All(引数 =>
        表示.Contains(引数.名前,StringComparison.OrdinalIgnoreCase)
        &&表示.Contains(引数.値,StringComparison.OrdinalIgnoreCase));
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

static long? 割当バイト数を取得(object? 報告) {
    var GC統計 = 報告?.GetType().GetProperty("GcStats")?.GetValue(報告);
    if(GC統計 is null) {
        return null;
    }

    var 合計取得メソッド = GC統計.GetType().GetMethod("GetTotalAllocatedBytes",new[] { typeof(bool) });
    var クォンタム込合計 = 非負整数に変換(合計取得メソッド?.Invoke(GC統計,new object[] { false }));
    var クォンタム除外合計 = 非負整数に変換(合計取得メソッド?.Invoke(GC統計,new object[] { true }));
    var 割当バイト数 = 非負整数に変換(GC統計.GetType().GetProperty("AllocatedBytes")?.GetValue(GC統計));
    foreach(var 値 in new[] { クォンタム込合計,割当バイト数,クォンタム除外合計 }) {
        if(値 is >0) {
            return 値;
        }
    }

    return クォンタム込合計>=0
        ? クォンタム込合計
        : 割当バイト数>=0
            ? 割当バイト数
            : クォンタム除外合計>=0
                ? クォンタム除外合計
                : null;
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

static void アフィニティを適用(int 論理CPU) {
    if(論理CPU<0||論理CPU>=Environment.ProcessorCount||論理CPU>=63) {
        return;
    }

    var マスク = 1L<<論理CPU;
    if(OperatingSystem.IsWindows()) {
        try {
            Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(マスク);
        } catch(PlatformNotSupportedException) {
        } catch(InvalidOperationException) {
        } catch(System.ComponentModel.Win32Exception) {
        }

        SetThreadAffinityMask(GetCurrentThread(),new UIntPtr((ulong)マスク));
    } else if(OperatingSystem.IsLinux()) {
        try {
            Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(マスク);
        } catch(PlatformNotSupportedException) {
        } catch(InvalidOperationException) {
        } catch(System.ComponentModel.Win32Exception) {
        }
    }
}

static void スレッドアフィニティを適用(int 論理CPU) {
    if(論理CPU<0||論理CPU>=Environment.ProcessorCount||論理CPU>=63||!OperatingSystem.IsWindows()) {
        return;
    }

    var マスク = 1UL<<論理CPU;
    SetThreadAffinityMask(GetCurrentThread(),new UIntPtr(マスク));
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
    double? 較正閾値ナノ秒 = null);

internal sealed record ベンチマーク引数(string 名前,string 値);

/// <summary>
/// 計測サンプルの本数。左右で必ず同じ値を使うため定数にしている。
/// 計測サンプル数は中央値が一意に決まるよう奇数にする。
/// </summary>
internal static class 計測設定 {
    public const int 破棄サンプル数 = 2;
    public const int 計測サンプル数 = 7;
}

/// <summary>1 サンプル（＝ループ回数 反復）を単位とした計測結果。時間はすべてループ合計ナノ秒。</summary>
internal sealed record 計測結果(
    double 中央値ナノ秒,
    double 誤差ナノ秒,
    double 標準偏差ナノ秒,
    int ループ回数,
    long 割り当てバイト数,
    double ダミー累積,
    IReadOnlyList<double> サンプル一覧);

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
    int? 実測ループ回数 = null);
internal sealed class 空ディスポーザブル:IDisposable {
    public static 空ディスポーザブル インスタンス { get; } = new();

    public void Dispose() {
    }
}

internal sealed class ワーカータスクフィルター(ワーカータスクメッセージ タスク):IFilter {
    public bool Predicate(BenchmarkDotNet.Running.BenchmarkCase ベンチマークケース) =>
        ベンチマークケース.Descriptor.WorkloadMethod.Name==タスク.メソッド名
        &&引数に一致する報告か(ベンチマークケース,タスク.引数);

    private static bool 引数に一致する報告か(BenchmarkDotNet.Running.BenchmarkCase ベンチマークケース,ベンチマーク引数[] 引数一覧) {
        if(引数一覧.Length==0) {
            return true;
        }
        var 表示 = ベンチマークケース.DisplayInfo;
        return 引数一覧.All(引数 =>
            表示.Contains(引数.名前,StringComparison.OrdinalIgnoreCase)
            &&表示.Contains(引数.値,StringComparison.OrdinalIgnoreCase));
    }
}
