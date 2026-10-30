using System.Diagnostics;
using System.Text.Json;
using Benchmark.Headless;
using UI;
using UI.Storage;

if(args.Contains("--verify-db-schema",StringComparer.OrdinalIgnoreCase)) {
    Environment.ExitCode=計測結果取込.スキーマを検査(args);
    return;
}

var cancellationToken = CancellationToken.None;
var catalog = ベンチマークカタログ.読み込む();
if(args.Contains("--import-jsonl",StringComparer.OrdinalIgnoreCase)) {
    Environment.ExitCode=計測結果取込.取り込む(args,catalog);
    return;
}
if(args.Contains("--list-json",StringComparer.OrdinalIgnoreCase)) {
    var listed=new List<object>();
    foreach(var source in ベンチマークソースタブ一覧.標準全て) {
        var cases=catalog.Where(item=>ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得を試みる(item.プロジェクト,item.アセンブリ名)?.タブ==source.タブ).ToArray();
        foreach(var group in cases.GroupBy(item=>(Type:TypeKey(item.型完全名),item.メソッド名,item.引数キー))) {
            var left=group.SingleOrDefault(item=>string.Equals(item.プロジェクト,source.プロジェクト順[0],StringComparison.OrdinalIgnoreCase));
            var right=group.SingleOrDefault(item=>string.Equals(item.プロジェクト,source.プロジェクト順[1],StringComparison.OrdinalIgnoreCase));
            if(left is null||right is null){continue;}
            listed.Add(new{Tab=source.ID,Type=group.Key.Type,Method=group.Key.メソッド名,Arguments=group.Key.引数キー});
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(listed));
    return;
}
var tabArgument = args.SkipWhile(static value => !string.Equals(value,"--run-tab",StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
var methodArgument = args.SkipWhile(static value => !string.Equals(value,"--method",StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
var typeArgument = args.SkipWhile(static value => !string.Equals(value,"--type",StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
var uiFastRun = args.Contains("--ui-fast-run",StringComparer.OrdinalIgnoreCase);
if(string.IsNullOrWhiteSpace(tabArgument)) {
    Console.WriteLine($"catalog-cases={catalog.Count}");
    return;
}

var definition = ベンチマークソースタブ一覧.IDから取得を試みる(tabArgument)
    ??throw new ArgumentException($"Unknown tab ID: {tabArgument}");
var tabCases = catalog.Where(item => ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得を試みる(item.プロジェクト,item.アセンブリ名)?.タブ==definition.タブ).ToArray();
var leftProject = definition.プロジェクト順[0];
var rightProject = definition.プロジェクト順[1];
var pairs = tabCases
    .GroupBy(item => (Type:TypeKey(item.型完全名),item.メソッド名,item.引数キー))
    .Select(group => new { FLinq=group.SingleOrDefault(item => string.Equals(item.プロジェクト,leftProject,StringComparison.OrdinalIgnoreCase)), Baseline=group.SingleOrDefault(item => string.Equals(item.プロジェクト,rightProject,StringComparison.OrdinalIgnoreCase)) })
    .Where(pair => pair.FLinq is not null&&pair.Baseline is not null)
    .Select(pair => (FLinq:pair.FLinq!,Baseline:pair.Baseline!));
if(!string.IsNullOrWhiteSpace(methodArgument)) pairs=pairs.Where(pair => string.Equals(pair.FLinq.メソッド名,methodArgument,StringComparison.Ordinal));
if(!string.IsNullOrWhiteSpace(typeArgument)) pairs=pairs.Where(pair => string.Equals(TypeKey(pair.FLinq.型完全名),typeArgument,StringComparison.Ordinal));
var pairArray = pairs.ToArray();
if(pairArray.Length==0) throw new InvalidOperationException($"No FLinq/baseline pairs for {definition.ID}.");
if(args.Contains("--dry-run",StringComparer.OrdinalIgnoreCase)) {
    Console.WriteLine($"tab={definition.ID} pairs={pairArray.Length} method={methodArgument ?? "<all>"}");
    return;
}

if(args.Contains("--multi-run",StringComparer.OrdinalIgnoreCase)) {
    Environment.ExitCode=await 複数実装計測.実行Async(definition,pairArray,args,cancellationToken);
    return;
}

var root = Path.Combine(Directory.GetCurrentDirectory(),"artifacts","paired-db",definition.ID.Replace(".","_").Replace("<","_").Replace(">","_"),DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));
Directory.CreateDirectory(root);
var measurements = new List<ペア測定>();
if(uiFastRun) {
    measurements.AddRange(await UI高速ペア測定を実行Async(pairArray,definition.タブ,root,cancellationToken));
} else {
    await using(var session = await ペア計測セッション.開始Async(root,cancellationToken)) {
        foreach(var pair in pairArray) {
            measurements.Add(new ペア測定(pair.FLinq,pair.Baseline,await session.計測Async(pair.FLinq,pair.Baseline,cancellationToken)));
        }
    }
}
if(args.Contains("--operator-harness",StringComparer.OrdinalIgnoreCase)) {
    if(measurements.Count!=1) throw new ArgumentException("--operator-harness requires exactly one pair.");
    Console.WriteLine(JsonSerializer.Serialize(new {
        measurements[0].値.FLinqNanoseconds,
        measurements[0].値.FLinqAllocation,
        measurements[0].値.BaselineNanoseconds,
        measurements[0].値.BaselineAllocation,
        measurements[0].値.FLinqSamples,
        measurements[0].値.FLinqAllocationSamples,
        measurements[0].値.BaselineSamples,
        measurements[0].値.BaselineAllocationSamples,
    }));
    return;
}
if(!args.Contains("--paired-db-run",StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Use --paired-db-run to save the same-worker paired measurements to Benchmark.db.");

var dbPath = Path.Combine(Directory.GetCurrentDirectory(),"Work","UI","Benchmark.db");
var store = new UI実行ストア(dbPath,UI実行アプリ種別.ベンチマーク);
var profile = uiFastRun ? ベンチマーク実行プロファイル.高速.ToString() : "PairedFast";
var targetNanoseconds = uiFastRun ? 1_000_000d : 10_000_000d;
var run = store.実行を作成(new(UI実行アプリ種別.ベンチマーク,$"Paired {definition.ID} {DateTimeOffset.UtcNow:O}",profile,definition.ID,DateTimeOffset.UtcNow));
store.実行設定を保存(run.実行ID,root,[1],targetNanoseconds,[0]);
UIベンチマークスナップショットケース書込 Case(ベンチマークケース item) => new(item.アセンブリ名,item.型完全名,item.メソッド名,item.引数キー,JsonSerializer.Serialize(item.引数一覧));
var entries = measurements.Select(item => new UIベンチマークスナップショット項目書込(definition.ID,TypeKey(item.FLinq.型完全名),item.FLinq.メソッド名,0,true,new UIベンチマークグリッド側書込(UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,item.値.FLinqNanoseconds,(long)Math.Round(item.値.FLinqAllocation),1,item.値.FLinqNanoseconds,true),new UIベンチマークグリッド側書込(UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,item.値.BaselineNanoseconds,(long)Math.Round(item.値.BaselineAllocation),1,item.値.BaselineNanoseconds,true),Case(item.FLinq),Case(item.Baseline))).ToArray();
store.ベンチマークスナップショット項目を保存(run.実行ID,entries);
store.実行状態を設定(run.実行ID,UI実行状態.完了,$"Paired completed pairs={entries.Length}");
Console.WriteLine($"saved-run={run.実行ID} tab={definition.ID} pairs={entries.Length}");

static string TypeKey(string typeName) => typeName[(typeName.LastIndexOf('.') + 1)..].Replace("IEnumerable",string.Empty,StringComparison.Ordinal).Replace("ValueLinq",string.Empty,StringComparison.Ordinal).Replace("FLinq",string.Empty,StringComparison.Ordinal).Replace("ZLinq",string.Empty,StringComparison.Ordinal).Replace("Linq",string.Empty,StringComparison.Ordinal);

static async Task<IReadOnlyList<ペア測定>> UI高速ペア測定を実行Async(
    IReadOnlyList<(ベンチマークケース FLinq,ベンチマークケース Baseline)> pairs,
    ベンチマークソースタブ tab,
    string root,
    CancellationToken cancellationToken) {
    var results = new List<ベンチマーク結果項目>();
    var service = new ベンチマーク実行サービス(new ベンチマークワーカープロセスサービス());
    await service.実行Async(
        pairs.SelectMany(static pair => new[] { pair.FLinq,pair.Baseline }).ToArray(),
        [0],
        static () => [0],
        [1],
        tab,
        ベンチマーク実行プロファイル.高速,
        TimeSpan.FromMilliseconds(1),
        true,
        root,
        null,
        results.Add,
        static _ => { },
        null,
        cancellationToken);
    return pairs.Select(pair => {
        var f = results.Last(result => string.Equals(result.プロジェクト,pair.FLinq.プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(result.型完全名,pair.FLinq.型完全名,StringComparison.Ordinal)
            &&string.Equals(result.メソッド,pair.FLinq.メソッド名,StringComparison.Ordinal)
            &&string.Equals(result.引数キー,pair.FLinq.引数キー,StringComparison.Ordinal));
        var b = results.Last(result => string.Equals(result.プロジェクト,pair.Baseline.プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(result.型完全名,pair.Baseline.型完全名,StringComparison.Ordinal)
            &&string.Equals(result.メソッド,pair.Baseline.メソッド名,StringComparison.Ordinal)
            &&string.Equals(result.引数キー,pair.Baseline.引数キー,StringComparison.Ordinal));
        var fTime=f.平均ナノ秒??double.NaN;
        var fAllocation=f.割り当てバイト数値??0;
        var bTime=b.平均ナノ秒??double.NaN;
        var bAllocation=b.割り当てバイト数値??0;
        return new ペア測定(pair.FLinq,pair.Baseline,(fTime,fAllocation,bTime,bAllocation,[fTime],[fAllocation],[bTime],[bAllocation]));
    }).ToArray();
}
sealed record ペア測定(
    ベンチマークケース FLinq,
    ベンチマークケース Baseline,
    (double FLinqNanoseconds,double FLinqAllocation,double BaselineNanoseconds,double BaselineAllocation,
        double[] FLinqSamples,long[] FLinqAllocationSamples,double[] BaselineSamples,long[] BaselineAllocationSamples) 値);

sealed class ペア計測セッション: IAsyncDisposable {
    private readonly Process worker;
    private ペア計測セッション(Process worker) => this.worker=worker;
    public static async Task<ペア計測セッション> 開始Async(string root,CancellationToken token) {
        var process=new Process { StartInfo=new ProcessStartInfo("dotnet") { UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true } };
        process.StartInfo.ArgumentList.Add("exec"); process.StartInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"Worker.dll")); process.StartInfo.ArgumentList.Add("--logical-cpu"); process.StartInfo.ArgumentList.Add("0"); process.StartInfo.ArgumentList.Add("--run-root"); process.StartInfo.ArgumentList.Add(root);
        if(!process.Start()||!string.Equals(await process.StandardOutput.ReadLineAsync(token),"ready",StringComparison.Ordinal)) throw new InvalidOperationException("Paired Worker could not be started.");
        return new ペア計測セッション(process);
    }
    public async Task<(double FLinqNanoseconds,double FLinqAllocation,double BaselineNanoseconds,double BaselineAllocation,
        double[] FLinqSamples,long[] FLinqAllocationSamples,double[] BaselineSamples,long[] BaselineAllocationSamples)> 計測Async(ベンチマークケース f,ベンチマークケース b,CancellationToken token) {
        var request=new { Command="paired-fast-run",Pair=new { FLinq=new { AssemblyName=f.アセンブリ名,TypeName=f.型完全名,MethodName=f.メソッド名,Arguments=f.引数一覧.Select(static x=>x.値).ToArray() },Baseline=new { AssemblyName=b.アセンブリ名,TypeName=b.型完全名,MethodName=b.メソッド名,Arguments=b.引数一覧.Select(static x=>x.値).ToArray() },ElementCount=4096,ElementScale="1",RandomSeed=1 } };
        await this.worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
        await this.worker.StandardInput.FlushAsync();
        using var json=JsonDocument.Parse(await this.worker.StandardOutput.ReadLineAsync(token)??throw new InvalidOperationException("Worker ended."));
        var fl=json.RootElement.GetProperty("FLinq");
        var bl=json.RootElement.GetProperty("Baseline");
        return(
            fl.GetProperty("中央値ナノ秒").GetDouble(),
            fl.GetProperty("割当バイト数").GetDouble(),
            bl.GetProperty("中央値ナノ秒").GetDouble(),
            bl.GetProperty("割当バイト数").GetDouble(),
            ReadDoubleSamples(fl,"ナノ秒サンプル"),
            ReadLongSamples(fl,"割当バイトサンプル"),
            ReadDoubleSamples(bl,"ナノ秒サンプル"),
            ReadLongSamples(bl,"割当バイトサンプル"));
    }
    private static double[] ReadDoubleSamples(JsonElement value,string name)=>value.GetProperty(name).EnumerateArray().Select(static sample=>sample.GetDouble()).ToArray();
    private static long[] ReadLongSamples(JsonElement value,string name)=>value.GetProperty(name).EnumerateArray().Select(static sample=>sample.GetInt64()).ToArray();
    public async ValueTask DisposeAsync() { if(!this.worker.HasExited) { await this.worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { Command="shutdown" })); await this.worker.StandardInput.FlushAsync(); await this.worker.WaitForExitAsync(); } this.worker.Dispose(); }
}

/// <summary>
/// --multi-run: FLinq・比較相手(較正役)・手書き理想の3者を、JIT最適化待機つきで同じ Worker で交互に計測し、
/// 1行1ケースの JSONL へ書く。Worker は型ごとに起動し直す(3アセンブリぶんのソースキャッシュを溜め続けないため)。
/// 引数: --logical-cpu &lt;n&gt;(既定 14)、--out &lt;jsonl&gt;(必須)、--seed &lt;n&gt;(既定 1、none でシードなし)、--rounds &lt;n&gt;(既定 9)。
/// </summary>
static class 複数実装計測 {
    public static async Task<int> 実行Async(ベンチマークソースタブ定義 タブ,IReadOnlyList<(ベンチマークケース FLinq,ベンチマークケース Baseline)> 組一覧,string[] 引数,CancellationToken キャンセル) {
        var 出力パス = 引数値(引数,"--out")??throw new ArgumentException("--multi-run requires --out <jsonl>.");
        var 論理CPU = int.Parse(引数値(引数,"--logical-cpu")??"14",System.Globalization.CultureInfo.InvariantCulture);
        var シード文字列 = 引数値(引数,"--seed")??"1";
        int? シード = null;
        if(!string.Equals(シード文字列,"none",StringComparison.OrdinalIgnoreCase)) {
            シード=int.Parse(シード文字列,System.Globalization.CultureInfo.InvariantCulture);
        }
        var ラウンド数 = int.Parse(引数値(引数,"--rounds")??"9",System.Globalization.CultureInfo.InvariantCulture);
        var 理想アセンブリ = 理想アセンブリ名(タブ.ID)??throw new ArgumentException("理想版の無いタブです: "+タブ.ID);
        var ディレクトリ = Path.GetDirectoryName(Path.GetFullPath(出力パス));
        if(ディレクトリ is not null) {
            Directory.CreateDirectory(ディレクトリ);
        }
        var 実行ルート = Path.Combine(Path.GetTempPath(),"seamless-multi-run");
        Directory.CreateDirectory(実行ルート);
        var 件数 = 0;
        await using var 書込 = new StreamWriter(出力パス,append: true,new System.Text.UTF8Encoding(false));
        foreach(var 型グループ in 組一覧.Where(組 => 理想型名(タブ,組.FLinq.型完全名) is not null).GroupBy(static 組 => 組.FLinq.型完全名)) {
            await using var Worker = await Workerを起動Async(論理CPU,実行ルート,キャンセル);
            foreach(var 組 in 型グループ) {
                var 理想型 = 理想型名(タブ,組.FLinq.型完全名)!;
                var 要求 = new {
                    Command="multi-fast-run",
                    Multi=new {
                        Implementations=new[] {
                            実装要求("FLinq",組.FLinq.アセンブリ名,組.FLinq.型完全名,組.FLinq),
                            実装要求("Baseline",組.Baseline.アセンブリ名,組.Baseline.型完全名,組.Baseline),
                            実装要求("Ideal",理想アセンブリ,理想型,組.FLinq),
                        },
                        CalibrationKey="Baseline",
                        ElementCount=4096,
                        ElementScale="1",
                        RandomSeed=シード,
                        Rounds=ラウンド数,
                        TargetSampleNs=10_000_000d,
                    },
                };
                var 応答 = await Worker.送受信Async(JsonSerializer.Serialize(要求),キャンセル);
                using var 結果 = JsonDocument.Parse(応答);
                using var バッファ = new MemoryStream();
                using(var 書き手 = new Utf8JsonWriter(バッファ)) {
                    書き手.WriteStartObject();
                    書き手.WriteString("Tab",タブ.ID);
                    書き手.WriteString("Type",組.FLinq.型完全名);
                    書き手.WriteString("Method",組.FLinq.メソッド名);
                    書き手.WriteString("ArgumentKey",組.FLinq.引数キー);
                    if(シード is { } 値) {
                        書き手.WriteNumber("Seed",値);
                    } else {
                        書き手.WriteNull("Seed");
                    }
                    書き手.WriteNumber("LogicalCpu",論理CPU);
                    書き手.WritePropertyName("Result");
                    結果.RootElement.WriteTo(書き手);
                    書き手.WriteEndObject();
                }
                await 書込.WriteLineAsync(System.Text.Encoding.UTF8.GetString(バッファ.ToArray()));
                await 書込.FlushAsync(キャンセル);
                件数++;
                Console.Error.WriteLine($"multi-run {タブ.ID} {件数}: {組.FLinq.型完全名} {組.FLinq.メソッド名}");
            }
        }
        Console.WriteLine($"multi-run tab={タブ.ID} rows={件数} out={出力パス}");
        return 0;
    }

    private static object 実装要求(string キー,string アセンブリ,string 型,ベンチマークケース ケース) => new {
        Key=キー,
        AssemblyName=アセンブリ,
        TypeName=型,
        MethodName=ケース.メソッド名,
        Arguments=ケース.引数一覧.Select(static 引数 => 引数.値).ToArray(),
    };

    /// <summary>タブごとの手書き理想版のアセンブリ。タブ定義の理想列から取る(理想列の無いタブは null)。</summary>
    public static string? 理想アセンブリ名(string タブID) {
        if(ベンチマークソースタブ一覧.IDから取得を試みる(タブID) is { 理想ありか: true } タブ) {
            return タブ.理想アセンブリ名;
        }
        return null;
    }

    /// <summary>
    /// 理想版の具象型名。単一演算子は FLinq の型名の FLinq を Ideal に置き換える(例 ….List.FLinq.FLinqInt32 → ….List.Ideal.IdealInt32)。
    /// 複合連鎖は型名が同じ(アセンブリだけ違う)。保存層(Persisted/Tsavorite)には理想版が無いので null。
    /// </summary>
    public static string? 理想型名(ベンチマークソースタブ定義 タブ,string FLinq型名) {
        if(FLinq型名.Contains("Persisted",StringComparison.Ordinal)||FLinq型名.Contains("Tsavorite",StringComparison.Ordinal)) {
            return null;
        }
        if(タブ.ID.StartsWith("Optimization.",StringComparison.Ordinal)) {
            return FLinq型名;
        }
        var 区切り = FLinq型名.LastIndexOf('.');
        var 名前空間 = FLinq型名[..区切り];
        var 型 = FLinq型名[(区切り+1)..];
        if(!名前空間.EndsWith(".FLinq",StringComparison.Ordinal)||!型.StartsWith("FLinq",StringComparison.Ordinal)) {
            return null;
        }
        return 名前空間[..^".FLinq".Length]+".Ideal.Ideal"+型["FLinq".Length..];
    }

    private static string? 引数値(string[] 引数,string 名前) =>
        引数.SkipWhile(値 => !string.Equals(値,名前,StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();

    private static async Task<Workerセッション> Workerを起動Async(int 論理CPU,string 実行ルート,CancellationToken キャンセル) {
        var プロセス = new Process { StartInfo=new ProcessStartInfo("dotnet") { UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true } };
        プロセス.StartInfo.ArgumentList.Add("exec");
        プロセス.StartInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"Worker.dll"));
        プロセス.StartInfo.ArgumentList.Add("--logical-cpu");
        プロセス.StartInfo.ArgumentList.Add(論理CPU.ToString(System.Globalization.CultureInfo.InvariantCulture));
        プロセス.StartInfo.ArgumentList.Add("--run-root");
        プロセス.StartInfo.ArgumentList.Add(実行ルート);
        if(!プロセス.Start()) {
            throw new InvalidOperationException("Worker を起動できませんでした。");
        }
        プロセス.ErrorDataReceived+=static (_,_) => { };
        プロセス.BeginErrorReadLine();
        if(!string.Equals(await プロセス.StandardOutput.ReadLineAsync(キャンセル),"ready",StringComparison.Ordinal)) {
            throw new InvalidOperationException("Worker が ready を返しませんでした。");
        }
        return new Workerセッション(プロセス);
    }

    private sealed class Workerセッション(Process プロセス):IAsyncDisposable {
        public async Task<string> 送受信Async(string 行,CancellationToken キャンセル) {
            await プロセス.StandardInput.WriteLineAsync(行);
            await プロセス.StandardInput.FlushAsync(キャンセル);
            return await プロセス.StandardOutput.ReadLineAsync(キャンセル)??throw new InvalidOperationException("Worker が終了しました。");
        }

        public async ValueTask DisposeAsync() {
            if(!プロセス.HasExited) {
                await プロセス.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { Command="shutdown" }));
                await プロセス.StandardInput.FlushAsync();
                await プロセス.WaitForExitAsync();
            }
            プロセス.Dispose();
        }
    }
}
