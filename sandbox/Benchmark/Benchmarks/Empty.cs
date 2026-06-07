using SystemEnumerable=global::System.Linq.Enumerable;

namespace Benchmark.Benchmarks;

public class Empty:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Empty<int>().Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        Enumerable.Empty<int>().Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.Empty<int>().Consume(consumer);
    }
}
