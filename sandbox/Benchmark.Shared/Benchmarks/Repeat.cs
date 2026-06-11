using SystemEnumerable=System.Linq.Enumerable;

namespace Benchmark.Benchmarks;

public class Repeat:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Repeat(7,1000000).Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        Enumerable.Repeat(7,1000000).Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.Repeat(7,1000000).Consume(this.consumer);
    }
}
