using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
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
            args=["--filter","*"];
        }
        var config=ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithIterationTime(TimeInterval.Millisecond*100)  // WorkloadPilot の収束目標
                .WithIterationCount(16)                           // WorkloadActual = 16
                .WithWarmupCount(4)                               // WorkloadWarmup = 4
            );
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,config);
    }
}
