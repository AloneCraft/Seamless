using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;

internal static class TpcETestStores{
    // 多数の主表と索引が既に確保していた約8MiB/poolを予算へ含める。
    // 行数・要求数は増やさない。小さいMemory試験ではnativeの巨大な将来リングを作らない。
    internal const long MemoryMegabytes=768;
    internal const long MemoryBytes=MemoryMegabytes<<20;
    private static StoreLimits Limits=>new(){MemoryBytes=MemoryBytes};
    internal static Store Temporary()=>Store.CreateTemporary(Limits);
    internal static Store Memory()=>Store.CreateMemory(Limits,options:new(){Growth=new Seamless.Execution.Growth.Policy<Seamless.Execution.Growth.Fixed>()});
    internal static Store Open(string path)=>Store.Open(path,Limits);
}
