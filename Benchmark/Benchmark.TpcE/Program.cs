using System.Diagnostics;
using System.Text.Json;
using Benchmark.TpcE.Core;
namespace Benchmark.TpcE;
/// <summary>TPC-E 模擬の負荷試験ベンチ。使い方は README.md。</summary>
public static class Program{
    public static int Main(string[] args){
        if(args.Length>0&&args[0]=="crash-child"){
            return CrashChildMain(args);
        }
        RunOptions options;
        try{
            options=RunOptions.Parse(args);
            FaultInjection.Parse(options.InjectFault);
        }catch(Exception error) when(error is ArgumentException or FormatException or OverflowException){
            Console.Error.WriteLine(error.Message);
            Console.Error.WriteLine("使い方: Benchmark.TpcE [run|generate|describe|replay] [--seed N] [--customers N] [--initial-trade-days N] [--transactions N] [--variants TL,TF,PL,PF] ...(README.md)");
            return 3;
        }
        ApplyProcessSettings(options);
        return options.Command switch{
            "run"=>Run(options),
            "generate"=>Generate(options),
            "describe"=>Describe(options),
            "replay"=>Replay(args),
            "crash"=>Crash(options),
            _=>Unknown(options.Command),
        };
    }
    /// <summary>強制終了の検査。既定は保存 300 CE ごと、組み合わせ TF と PF。</summary>
    private static int Crash(RunOptions options){
        if(!Environment.GetCommandLineArgs().Contains("--save-every")){
            options=options with{SaveEvery=300};
        }
        if(!Environment.GetCommandLineArgs().Contains("--variants")){
            options=options with{Variants=["TF","PF"]};
        }
        var variants=new List<ITpcEVariant>();
        foreach(var name in options.Variants){
            variants.Add(VariantCatalog.ByShortName(name));
        }
        var runDirectory=WorkDirectory.CreateRunDirectory(WorkDirectory.Resolve(options.WorkDirectory),options);
        File.WriteAllText(Path.Combine(runDirectory,"run.json"),JsonSerializer.Serialize(new{Options=options,Arguments=options.ToArguments(),Started=DateTimeOffset.Now,Commit=GitCommit()},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"実行フォルダ: {runDirectory}");
        return new CrashCampaign(options,variants,runDirectory,Console.Out,CrashCampaign.ResolveChildCommand()).Run();
    }
    private static int CrashChildMain(string[] args){
        var arguments=CrashChild.Parse(args);
        ApplyProcessSettings(arguments.Options);
        return CrashChild.Run(arguments,VariantCatalog.ByShortName(arguments.Variant),Console.Out);
    }
    private static int Unknown(string command){
        Console.Error.WriteLine($"Unknown command {command}.");
        return 3;
    }
    /// <summary>既定は他の作業を邪魔しない(優先度を下げる)。--cpu を指定したときだけ 1 つの論理 CPU に固定する。</summary>
    private static void ApplyProcessSettings(RunOptions options){
        var process=Process.GetCurrentProcess();
        process.PriorityClass=options.Priority.ToLowerInvariant() switch{
            "normal"=>ProcessPriorityClass.Normal,
            "idle"=>ProcessPriorityClass.Idle,
            _=>ProcessPriorityClass.BelowNormal,
        };
        if(options.Cpu is int cpu&&OperatingSystem.IsWindows()){
            process.ProcessorAffinity=(IntPtr)(1L<<cpu);
        }
    }
    private static int Run(RunOptions options){
        var variants=new List<ITpcEVariant>();
        foreach(var name in options.Variants){
            variants.Add(VariantCatalog.ByShortName(name));
        }
        var runDirectory=WorkDirectory.CreateRunDirectory(WorkDirectory.Resolve(options.WorkDirectory),options);
        File.WriteAllText(Path.Combine(runDirectory,"run.json"),JsonSerializer.Serialize(new{Options=options,Arguments=options.ToArguments(),Started=DateTimeOffset.Now,Commit=GitCommit()},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"実行フォルダ: {runDirectory}");
        if(variants.Count==1){
            Console.WriteLine("注意: 組み合わせが 1 つだけなので、組み合わせ同士の突き合わせはしません(読み戻しと保存→開き直しだけ)。");
        }
        var runner=new LockstepRunner(options,variants,runDirectory,Console.Out);
        Console.CancelKeyPress+=(_,e)=>{
            e.Cancel=true;
            runner.RequestStop();
        };
        var outcome=runner.Run();
        return outcome.ExitCode;
    }
    private static int Generate(RunOptions options){
        var watch=Stopwatch.StartNew();
        var sink=new CountingLoadSink();
        var result=new TpcEDataGenerator(new TpcEModel(options.Seed,options.Scale)).Generate(sink);
        Console.WriteLine($"生成: {watch.Elapsed.TotalSeconds:F1} 秒、初期取引 {result.LastInitialTradeId:N0} 件、未約定 {result.UnfinishedTrades} 件");
        var names=new List<string>(sink.Tables.Keys);
        names.Sort(StringComparer.Ordinal);
        foreach(var name in names){
            Console.WriteLine($"  {sink.Tables[name].Build()}");
        }
        return 0;
    }
    private static int Describe(RunOptions options){
        var scale=options.Scale;
        Console.WriteLine($"規模: 顧客 {scale.Customers:N0}、口座 約 {scale.Accounts:N0}、会社 {scale.Companies:N0}、銘柄 {scale.Securities:N0}、ブローカー {scale.Brokers:N0}");
        Console.WriteLine($"初期取引: {scale.InitialTradeDays} 営業日 × {TpcESpec.SecondsPerBusinessDay:N0} 秒 × {scale.Customers}/{TpcESpec.ScaleFactor} = {scale.InitialTrades:N0} 件");
        Console.WriteLine($"日次相場: {scale.MarketDays} 営業日 × {scale.Securities:N0} 銘柄、ニュース本文 {scale.NewsItemBytes:N0} バイト");
        Console.WriteLine("推測で決めた値:");
        foreach(var assumption in TpcEAssumptions.All){
            Console.WriteLine("  - "+assumption);
        }
        Console.WriteLine("FLinq を止めた問い合わせ([LinqIntercept(false)]):");
        foreach(var variant in VariantCatalog.All){
            if(!variant.Name.EndsWith("-flinq",StringComparison.Ordinal)){
                continue;
            }
            var missing=InterceptionCoverage.FindMissing(variant.GetType().Assembly,out var stopped);
            Console.WriteLine($"  {variant.Name}: 止めた {stopped.Count} 件 {string.Join(", ",stopped)}、置き換え漏れ {missing.Count} 件 {string.Join(", ",missing)}");
        }
        return 0;
    }
    /// <summary>replay --from 実行フォルダ: その実行の引数で、失敗の直前まで流して詳しく記録する。</summary>
    private static int Replay(string[] args){
        var from=Array.IndexOf(args,"--from");
        if(from<0||from+1>=args.Length){
            Console.Error.WriteLine("replay --from <実行フォルダ> を指定してください。");
            return 3;
        }
        var directory=args[from+1];
        var run=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"run.json")));
        var original=new List<string>();
        foreach(var item in run.RootElement.GetProperty("Arguments").EnumerateArray()){
            original.Add(item.GetString()!);
        }
        var options=RunOptions.Parse(original) with{Command="run"};
        var failurePath=Path.Combine(directory,"failure","failure.json");
        if(File.Exists(failurePath)){
            var txn=JsonDocument.Parse(File.ReadAllText(failurePath)).RootElement.GetProperty("TxnIndex").GetInt64();
            options=options with{StopAt=txn+1,TraceFrom=Math.Max(1,txn-20)};
        }
        Console.WriteLine("再実行: "+options.ToCommandLine());
        return Run(options);
    }
    private static string? GitCommit(){
        try{
            var head=Path.Combine(WorkDirectory.RepositoryRoot(),".git","HEAD");
            if(!File.Exists(head)){
                return null;
            }
            var text=File.ReadAllText(head).Trim();
            if(text.StartsWith("ref: ",StringComparison.Ordinal)){
                var reference=Path.Combine(WorkDirectory.RepositoryRoot(),".git",text[5..]);
                if(File.Exists(reference)){
                    return File.ReadAllText(reference).Trim();
                }
            }
            return text;
        }catch(IOException){
            return null;
        }
    }
}
