using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

[BenchmarkTestIterations(17)]
public class Range():Base(11_200_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        const int rangeCount=1_000_000;
        SystemEnumerable.Range(1,rangeCount).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        const int rangeCount=1_000_000;
        ValueEnumerable.Range(1,rangeCount).Consume(this.consumer);
    }
}
