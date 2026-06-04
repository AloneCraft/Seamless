using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

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
        IConfig? config;
        if(Environment.GetEnvironmentVariable("BDN_DEBUG")=="1"){
            config=new CustomBenchmarkConfig();
        }else{
            config=null;
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,config);
    }
}

public class CustomBenchmarkConfig : ManualConfig{
    public CustomBenchmarkConfig(){
        WithOption(ConfigOptions.DisableOptimizationsValidator, true);
        AddJob(Job.Default
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithWarmupCount(1)
            .WithIterationCount(3)
            .WithInvocationCount(16)
            .WithLaunchCount(1)
        );
        AddDiagnoser(MemoryDiagnoser.Default);
    }
}
