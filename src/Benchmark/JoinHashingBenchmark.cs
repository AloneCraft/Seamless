using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Seamless.Carrier;
using SystemEnumerable = System.Linq.Enumerable;

namespace BenchmarkExe.Benchmarks;

// ルール §3: ハッシュ結合分解 + キーセレクタ・ホイスト。
// outer 100k / inner 1k。各 outer はちょうど 1 件の inner と一致（lookup 構築が効く規模）。
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
public class JoinHashingBenchmark
{
    private int[] outer = null!;
    private int[] inner = null!;
    private OptimizationBenchmarkContainer container = null!;

    [Params(100_000)]
    public int CountValue { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.outer = SystemEnumerable.Range(0, this.CountValue).ToArray();
        this.inner = SystemEnumerable.Range(0, 1_000).ToArray();
        this.container = new OptimizationBenchmarkContainer();
    }

    [Benchmark(Baseline = true)]
    public int SystemLinqJoinSum()
    {
        return SystemEnumerable.Sum(
            SystemEnumerable.Join(
                this.outer,
                this.inner,
                static o => o % 1000,
                static i => i,
                static (o, i) => o + i));
    }

    [Benchmark]
    public int InterceptedContainerJoinSum()
    {
        return this.container.JoinSum(this.outer, this.inner);
    }

    [Benchmark]
    public int CarrierDirectJoinSum()
    {
        return this.outer.AsValueEnumerable()
            .Join((IEnumerable<int>)this.inner,
                static o => o % 1000,
                static i => i,
                static (o, i) => o + i)
            .Sum();
    }
}
