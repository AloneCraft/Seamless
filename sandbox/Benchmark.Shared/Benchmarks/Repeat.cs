using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

[BenchmarkTestIterations(16)]
public class Repeat():Base(20_000_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        const int repeatCount=1_000_000;
        SystemEnumerable.Repeat(7,repeatCount).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        const int repeatCount=1_000_000;
        ValueEnumerable.Repeat(7,repeatCount).Consume(this.consumer);
    }
}
