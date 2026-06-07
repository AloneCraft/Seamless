using SystemEnumerable=global::System.Linq.Enumerable;

namespace Benchmark.Benchmarks;

public class Range:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Range(1,1000000).Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        Enumerable.Range(1,1000000).Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.Range(1,1000000).Consume(this.consumer);
    }
}
