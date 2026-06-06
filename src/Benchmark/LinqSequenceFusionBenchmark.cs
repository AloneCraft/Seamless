using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Seamless.Linq.Carrier;
using SystemEnumerable = System.Linq.Enumerable;

namespace BenchmarkExe.Benchmarks;

static class DatabaseGenerator;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1,iterationCount: 5)]
public class LinqSequenceFusionBenchmark {
    private int[] outer = null!;
    private int[] inner = null!;
    private LinqSequenceFusionContainer baselineContainer = null!;

    [Params(10_000)]
    public int CountValue { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        this.outer=SystemEnumerable.Range(0,this.CountValue).ToArray();
        this.inner=SystemEnumerable.Range(0,128).ToArray();
        this.baselineContainer=new LinqSequenceFusionContainer();
    }

    [Benchmark(Baseline = true)]
    public int SystemLinqWhereSelectSum() {
        return SystemEnumerable.Sum(
            SystemEnumerable.Select(
                SystemEnumerable.Where(this.outer,static value => value>10),
                static value => value*2));
    }

    [Benchmark]
    public int InterceptedContainerWhereSelectSum() {
        return this.baselineContainer.WhereSelectSum(this.outer);
    }

    [Benchmark]
    public int CarrierDirectWhereSelectSum() {
        return this.outer.AsValueEnumerable()
            .Where(static value => value>10)
            .Select(static value => value*2)
            .Sum();
    }

    [Benchmark]
    public int SystemLinqSelectManySum() {
        return SystemEnumerable.Sum(
            SystemEnumerable.SelectMany(
                this.outer,
                _ => this.inner,
                static (outerValue,innerValue) => outerValue+innerValue));
    }

    [Benchmark]
    public int InterceptedContainerSelectManySum() {
        return this.baselineContainer.SelectManySum(this.outer,this.inner);
    }

    [Benchmark]
    public int CarrierDirectSelectManySum() {
        var inner = this.inner;
        return this.outer.AsValueEnumerable()
            .SelectMany(_ => (IEnumerable<int>)inner,static (o,i) => o+i)
            .Sum();
    }

    [Benchmark]
    public int SystemLinqJoinSelectSum() {
        return SystemEnumerable.Sum(
            SystemEnumerable.Select(
                SystemEnumerable.Join(
                    this.outer,
                    this.inner,
                    static outerValue => outerValue&127,
                    static innerValue => innerValue,
                    static (outerValue,innerValue) => (outerValue,innerValue)),
                static pair => pair.outerValue+pair.innerValue));
    }

    [Benchmark]
    public int InterceptedContainerJoinSelectSum() {
        return SumSequence(this.baselineContainer.JoinSelectSequence(this.outer,this.inner));
    }

    [Benchmark]
    public int CarrierDirectJoinSelectSum() {
        return this.outer.AsValueEnumerable()
            .Join((IEnumerable<int>)this.inner,
                static o => o&127,
                static i => i,
                static (o,i) => o+i)
            .Sum();
    }

    private static int SumSequence(IEnumerable<int> source) {
        var sum = 0;
        foreach(var value in source) {
            sum+=value;
        }
        return sum;
    }
}

public class LinqSequenceFusionContainer {
    public virtual int WhereSelectSum(IEnumerable<int> outer)
        => outer.Where(static value => value>10).Select(static value => value*2).Sum();
    public virtual int SelectManySum(IEnumerable<int> outer,IEnumerable<int> inner)
        => outer.SelectMany(_ => inner,static (outerValue,innerValue) => outerValue+innerValue).Sum();
    public virtual IEnumerable<int> JoinSelectSequence(IEnumerable<int> outer,IEnumerable<int> inner)
        => outer.Join(inner,static outerValue => outerValue&127,static innerValue => innerValue,
               static (outerValue,innerValue) => outerValue+innerValue);
}
