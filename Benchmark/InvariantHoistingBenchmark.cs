using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Tsavorite.Carrier;
using SystemEnumerable = System.Linq.Enumerable;

namespace Tsavorite.Benchmark.Benchmarks;

// ルール §1: ループ不変式�EイスチE+ delegate/closure 除去、E// baseline は N 囁EHoistInvariant.Value() を呼び lambda めEdelegate 経由で起動する、E// intercept 版�E struct 融合で delegate 割当を消し、不変式を巻き上げられる経路を測る、E[MemoryDiagnoser]
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
