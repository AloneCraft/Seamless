using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

public class Empty:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Empty<int>().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.Empty<int>().Consume(this.consumer);
    }
}
