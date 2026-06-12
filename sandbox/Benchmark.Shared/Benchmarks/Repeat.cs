using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

public class Repeat:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Repeat(7,1000000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.Repeat(7,1000000).Consume(this.consumer);
    }
}
