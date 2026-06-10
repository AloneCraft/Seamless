using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Tsavorite.Carrier;
using SystemEnumerable = System.Linq.Enumerable;

namespace BenchmarkExe.Benchmarks;

// Distinct / Union の容量事前確保（GetCapacityHint）の効果を測る。
// source が配列（TryGetNonEnumeratedCount==true）の場合、HashSetSlim の resize 回数が減る。
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
public class SetOperationCapacityBenchmark
{
    private int[] source = null!;
    private int[] second = null!;

    [Params(10_000)]
    public int CountValue { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // source は半分が重複（distinct 結果は CountValue/2 程度）
        var half = Math.Max(1, this.CountValue / 2);
        this.source = new int[this.CountValue];
        for (var i = 0; i < this.CountValue; i++)
        {
            this.source[i] = i % half;
        }
        this.second = SystemEnumerable.ToArray(SystemEnumerable.Range(this.CountValue / 2, this.CountValue));
    }

    [Benchmark(Baseline = true)]
    public int SystemLinqDistinctCount()
    {
        return SystemEnumerable.Count(SystemEnumerable.Distinct(this.source));
    }

    [Benchmark]
    public int CarrierDistinctCount()
    {
        return this.source.AsValueEnumerable().Distinct().Count();
    }

    [Benchmark]
    public int SystemLinqUnionCount()
    {
        return SystemEnumerable.Count(SystemEnumerable.Union(this.source, this.second));
    }

    [Benchmark]
    public int CarrierUnionCount()
    {
        return this.source.AsValueEnumerable().Union((IEnumerable<int>)this.second).Count();
    }
}
