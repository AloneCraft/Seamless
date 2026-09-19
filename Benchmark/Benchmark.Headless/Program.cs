using System.Diagnostics;
using System.Text.Json;
using Benchmark.Headless;
using UI;
using UI.Storage;

if(args.Contains("--performance-gate",StringComparer.OrdinalIgnoreCase)) {
    Environment.ExitCode=await PerformanceGateRunner.実行Async(args,CancellationToken.None);
    return;
}

var cancellationToken = CancellationToken.None;
var catalog = ベンチマークカタログ.読み込む();
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
