using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Tsavorite.Carrier;
using SystemEnumerable = System.Linq.Enumerable;

namespace BenchmarkExe.Benchmarks;

// ルール §1: ループ不変式ホイスト + delegate/closure 除去。
// baseline は N 回 HoistInvariant.Value() を呼び lambda を delegate 経由で起動する。
// intercept 版は struct 融合で delegate 割当を消し、不変式を巻き上げられる経路を測る。
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
public class InvariantHoistingBenchmark
{
    private int[] outer = null!;
    private OptimizationBenchmarkContainer container = null!;

    [Params(100_000)]
    public int CountValue { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.outer = SystemEnumerable.Range(0, this.CountValue).ToArray();
        this.container = new OptimizationBenchmarkContainer();
    }

    [Benchmark(Baseline = true)]
    public int SystemLinqSelectInvariantSum()
    {
        return SystemEnumerable.Sum(
            SystemEnumerable.Select(this.outer, static value => value + HoistInvariant.Value()));
    }

    [Benchmark]
    public int InterceptedContainerSelectInvariantSum()
    {
        return this.container.SelectInvariantSum(this.outer);
    }

    [Benchmark]
    public int CarrierDirectSelectInvariantSum()
    {
        return this.outer.AsValueEnumerable()
            .Select(static value => value + HoistInvariant.Value())
            .Sum();
    }
}
