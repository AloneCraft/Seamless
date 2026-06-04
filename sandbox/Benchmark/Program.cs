using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmark;

internal static class Program{
    public static void Main(string[] args){
        var config=Environment.GetEnvironmentVariable("BDN_DEBUG")=="1"
            ?(IConfig)new DebugInProcessConfig()
            :null;
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,config);
    }
}
