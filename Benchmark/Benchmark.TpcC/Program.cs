using System.Diagnostics;
using System.Text.Json;
using Benchmark.TpcC.Core;
namespace Benchmark.TpcC;
/// <summary>TPC-C 由来の負荷試験ベンチ。使い方は README.md。</summary>
public static class Program{
    public static int Main(string[] args){
        try{
            return MainCore(args);
        }catch(Exception error) when(error is ArgumentException or FormatException or OverflowException or JsonException){
            Console.Error.WriteLine(error.Message);
            return 3;
        }catch(Exception error){
            Console.Error.WriteLine(error);
            return 2;
        }
    }
    private static int MainCore(string[] args){
        Console.OutputEncoding=System.Text.Encoding.UTF8;
        if(args.Length>0&&args[0]=="replay"){
            return Replay(args);
        }
        if(args.Length>0&&args[0]=="crash-child"){
            return TpcCCrashChildMain(args);
        }
        TpcCRunOptions options;
        try{
            options=TpcCRunOptions.Parse(args);
            TpcCFaultInjection.Parse(options.InjectFault);
        }catch(Exception error) when(error is ArgumentException or FormatException or OverflowException){
            Console.Error.WriteLine(error.Message);
            Console.Error.WriteLine("使い方: Benchmark.TpcC [run|generate|describe|replay] [--seed N] [--warehouses N] [--partitions N] [--transactions N] [--variants TL,TF,PL,PF] ...(README.md)");
            return 3;
        }
        if(options.Command=="crash"){
            if(!args.Contains("--save-every")){
                options=options with{SaveEvery=300};
            }
            if(!args.Contains("--variants")){
                options=options with{Variants=["TF","PF"]};
            }
        }
        ApplyProcessSettings(options);
        return options.Command switch{
            "run"=>Run(options),
            "generate"=>Generate(options),
            "describe"=>Describe(options),
            "crash"=>Crash(options),
            _=>Unknown(options.Command),
        };
    }
    /// <summary>強制終了の検査。既定は保存 300 取引 ごと、組み合わせ TF と PF。</summary>
    private static int Crash(TpcCRunOptions options){
        var variants=new List<ITpcCVariant>();
        foreach(var name in options.Variants){
            variants.Add(VariantCatalog.ByShortName(name));
        }
        var runDirectory=TpcCWorkDirectory.CreateRunDirectory(TpcCWorkDirectory.Resolve(options.WorkDirectory),options);
        File.WriteAllText(Path.Combine(runDirectory,"run.json"),JsonSerializer.Serialize(new{Options=options,Arguments=options.ToArguments(),Started=DateTimeOffset.Now,Commit=GitCommit()},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"実行フォルダ: {runDirectory}");
        return new TpcCCrashCampaign(options,variants,runDirectory,Console.Out,TpcCCrashCampaign.ResolveChildCommand()).Run();
    }
    private static int TpcCCrashChildMain(string[] args){
        var arguments=TpcCCrashChild.Parse(args);
        ApplyProcessSettings(arguments.Options);
        return TpcCCrashChild.Run(arguments,VariantCatalog.ByShortName(arguments.Variant),Console.Out);
    }
    private static int Unknown(string command){
        Console.Error.WriteLine($"Unknown command {command}.");
        return 3;
    }
    /// <summary>既定は他の作業を邪魔しない(優先度を下げる)。--cpu を指定したときだけ 1 つの論理 CPU に固定する。</summary>
    private static void ApplyProcessSettings(TpcCRunOptions options){
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
    private static int Run(TpcCRunOptions options){
        var variants=new List<ITpcCVariant>();
        foreach(var name in options.Variants){
            variants.Add(VariantCatalog.ByShortName(name));
        }
        var runDirectory=TpcCWorkDirectory.CreateRunDirectory(TpcCWorkDirectory.Resolve(options.WorkDirectory),options);
        File.WriteAllText(Path.Combine(runDirectory,"run.json"),JsonSerializer.Serialize(new{Options=options,Arguments=options.ToArguments(),Started=DateTimeOffset.Now,Commit=GitCommit()},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"実行フォルダ: {runDirectory}");
        if(variants.Count==1){
            Console.WriteLine("注意: 組み合わせが 1 つだけなので、組み合わせ同士の突き合わせはしません(読み戻しと保存→開き直しだけ)。");
        }
        var runner=new TpcCLockstepRunner(options,variants,runDirectory,Console.Out);
        ConsoleCancelEventHandler cancel=(_,e)=>{
            e.Cancel=true;
            runner.RequestStop();
        };
        Console.CancelKeyPress+=cancel;
        try{
            return runner.Run().ExitCode;
        }finally{
            Console.CancelKeyPress-=cancel;
        }
    }
    private static int Generate(TpcCRunOptions options){
        var watch=Stopwatch.StartNew();
        var sink=new TpcCCountingLoadSink();
        new TpcCDataGenerator(options.Scale,TpcCLoadRules.For(options.Mode),options.Seed).Generate(sink);
        Console.WriteLine($"生成: {watch.Elapsed.TotalSeconds:F1} 秒");
        Console.WriteLine(JsonSerializer.Serialize(sink,new JsonSerializerOptions{WriteIndented=true}));
        return 0;
    }
    private static int Describe(TpcCRunOptions options){
        Console.WriteLine("TPC-C 由来・非監査・TPC-C の結果と比較不可");
        Console.WriteLine($"規模: 倉庫 {options.Warehouses:N0}、区画 {options.Scale.PartitionCount:N0}、品目 {options.Items:N0}、地区あたり顧客 {options.CustomersPerDistrict:N0}、端末 {options.Terminals:N0}");
        Console.WriteLine($"規則: {TpcCRunOptions.ModeName(options.Mode)}、組み合わせ: {string.Join(",",options.Variants)}");
        Console.WriteLine("run は正しさの検査を含む直列実行です。計測専用実行や公式の結果には使えません。");
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
        using var run=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"run.json")));
        var original=new List<string>();
        foreach(var item in run.RootElement.GetProperty("Arguments").EnumerateArray()){
            original.Add(item.GetString()!);
        }
        var options=TpcCRunOptions.Parse(original);
        ApplyProcessSettings(options);
        if(options.Command=="crash"){
            Console.WriteLine("強制終了の検査を同じ種と引数で再実行します(時間停止の位置は変動します)。");
            return Crash(options);
        }
        options=options with{Command="run"};
        var failurePath=Path.Combine(directory,"failure","failure.json");
        if(File.Exists(failurePath)){
            using var failure=JsonDocument.Parse(File.ReadAllText(failurePath));
            var txn=failure.RootElement.GetProperty("TxnIndex").GetInt64();
            options=options with{StopAt=txn+1,TraceFrom=Math.Max(1,txn-20)};
        }
        Console.WriteLine("再実行: "+options.ToCommandLine());
        return Run(options);
    }
    private static string? GitCommit(){
        try{
            var gitDirectory=Path.Combine(TpcCWorkDirectory.RepositoryRoot(),".git");
            if(File.Exists(gitDirectory)){
                var pointer=File.ReadAllText(gitDirectory).Trim();
                if(!pointer.StartsWith("gitdir: ",StringComparison.Ordinal)){
                    return null;
                }
                gitDirectory=Path.GetFullPath(pointer[8..],TpcCWorkDirectory.RepositoryRoot());
            }
            var head=Path.Combine(gitDirectory,"HEAD");
            if(!File.Exists(head)){
                return null;
            }
            var text=File.ReadAllText(head).Trim();
            if(text.StartsWith("ref: ",StringComparison.Ordinal)){
                var reference=Path.Combine(gitDirectory,text[5..]);
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
