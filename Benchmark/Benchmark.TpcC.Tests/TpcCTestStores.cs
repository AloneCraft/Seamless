using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Tests;

internal static class TpcCTestStores{
    // Tsavoriteの表と索引は1つごとに約8MiBの溢れpoolを確保しており、予算はそれを含めて数える。
    // 既定の256MiBでは区画を分けた全表を開けないので、TPC-Eの試験と同じ768MiBにする。行数・要求数は増やさない。
    internal const long MemoryMegabytes=768;
    private static StoreLimits Limits=>new(){MemoryBytes=MemoryMegabytes<<20};
    internal static Store Temporary()=>Store.CreateTemporary(Limits);
    internal static Store Open(string path)=>Store.Open(path,Limits);
}
