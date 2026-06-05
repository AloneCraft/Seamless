using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Perfolizer.Horology;

namespace Benchmark;

internal static class Program{
    public static void Main(string[] args){
        // ダブルクリック起動時など CWD が不定の場合、BDN が .csproj を広範検索して
        // macOS の .Trash 等アクセス拒否ディレクトリに当たるのを防ぐ
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent){
            if(d.GetFiles("*.csproj").Length>0){
                Directory.SetCurrentDirectory(d.FullName);
                break;
            }
        }
        if(args.Length==0){
            args=SelectBenchmarks();
            //args=["--filter","*"];// デバッグ用全選択
        }
        var job=Job.Default
            .WithIterationTime(TimeInterval.Millisecond*100)  // WorkloadPilot の収束目標
            .WithIterationCount(16)                           // WorkloadActual = 16
            .WithWarmupCount(4);                              // WorkloadWarmup = 4
        if(Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_INPROCESS")=="1"){
            job=job.WithToolchain(InProcessNoEmitToolchain.Instance);
        }
        var config=ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job);
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,config);
    }

    static string[] SelectBenchmarks(){
        var benchmarkTypes=typeof(Program).Assembly.GetTypes()
            .Where(t=>t.GetCustomAttributes(typeof(BenchmarkAttribute),false).Length>0)
            .OrderBy(t=>t.Name)
            .ToList();

        if(benchmarkTypes.Count==0){
            return ["--filter","*"];
        }

        Console.WriteLine("=== ベンチマーク選択 ===");
        Console.WriteLine("番号を選択してください (カンマ区切りで複数選択可、Enterで全て実行):\n");

        for(int i=0;i<benchmarkTypes.Count;i++){
            Console.WriteLine($"{i+1,2}: {benchmarkTypes[i].Name}");
        }

        Console.Write("\n選択 > ");
        var input=Console.ReadLine()?.Trim()??string.Empty;

        if(string.IsNullOrEmpty(input)){
            return ["--filter","*"];
        }

        var selected=new List<string>();
        var indices=input.Split(',');
        foreach(var idx in indices){
            if(int.TryParse(idx.Trim(),out var n)&&n>=1&&n<=benchmarkTypes.Count){
                selected.Add(benchmarkTypes[n-1].Name);
            }
        }

        if(selected.Count==0){
            return ["--filter","*"];
        }

        var pattern=string.Join("|",selected.Select(s=>$"*{s}*"));
        return ["--filter",pattern];
    }
}
