using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

public class Range:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Range(1,1000000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.Range(1,1000000).Consume(this.consumer);
    }
}
