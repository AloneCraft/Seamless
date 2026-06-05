using SystemEnumerable=global::System.Linq.Enumerable;

namespace Benchmark;

public class Range:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Range(1,1000000).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        Enumerable.Range(1,1000000).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.Range(1,1000000).Consume(consumer);
    }
}
