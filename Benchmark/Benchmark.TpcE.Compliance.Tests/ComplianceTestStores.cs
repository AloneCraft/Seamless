using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance.Tests;

internal static class ComplianceTestStores{
    // Tsavoriteの表と索引は1つごとに約8MiBの溢れpoolを確保しており、予算はそれを含めて数える。
    // EGenの初期データの一括投入は768MiBでも予算を超える(2048MiBで全件通ることを確認)。行数・要求数は増やさない。
    internal const long MemoryMegabytes=2048;
    private static StoreLimits Limits=>new(){MemoryBytes=MemoryMegabytes<<20};
    internal static Store Temporary()=>Store.CreateTemporary(Limits);
    internal static Store Open(string path)=>Store.Open(path,Limits);
}
