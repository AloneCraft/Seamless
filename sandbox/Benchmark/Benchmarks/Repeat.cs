using SystemEnumerable=global::System.Linq.Enumerable;

namespace Benchmark;

public class Repeat:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Repeat(7,1000000).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        Enumerable.Repeat(7,1000000).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.Repeat(7,1000000).Consume(consumer);
    }
}
