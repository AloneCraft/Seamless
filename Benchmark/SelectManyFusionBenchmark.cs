using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Tsavorite.Carrier;
using SystemEnumerable = System.Linq.Enumerable;

namespace Tsavorite.Benchmark.Benchmarks;

// ルール §2: SelectMany 融合 + 述語�EチE��ュダウン、E// 選択的な Where�E�Ehreshold 高め�E�で Cartesian 積を O(N*M) から O(N*K) へ縮められる経路を測る、E[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
public class SelectManyFusionBenchmark
{
    private int[] outer = null!;
    private int[] inner = null!;
    private int threshold;
    private OptimizationBenchmarkContainer container = null!;

    [Params(100_000)]
    public int CountValue { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.outer = SystemEnumerable.Range(0, this.CountValue).ToArray();
        this.inner = SystemEnumerable.Range(0, 128).ToArray();
        this.threshold = 120; // inner 128 件中 K=7 件だけ通過、E        this.container = new OptimizationBenchmarkContainer();
    }

    [Benchmark(Baseline = true)]
    public int SystemLinqSelectManyWhereSum()
    {
        var local = this.inner;
        var limit = this.threshold;
        return SystemEnumerable.Sum(
            SystemEnumerable.Where(
                SystemEnumerable.SelectMany(this.outer, _ => local),
                value => value > limit));
    }

    [Benchmark]
    public int InterceptedContainerSelectManyWhereSum()
    {
        return this.container.SelectManyWhereSum(this.outer, this.inner, this.threshold);
    }

    [Benchmark]
    public int CarrierDirectSelectManyWhereSum()
    {
        var local = this.inner;
        var limit = this.threshold;
        return this.outer.AsValueEnumerable()
            .SelectMany(_ => (IEnumerable<int>)local)
            .Where(value => value > limit)
            .Sum();
    }
}
