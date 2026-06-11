using SystemEnumerable=System.Linq.Enumerable;

namespace Benchmark.Benchmarks;

public class Empty:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.Empty<int>().Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        Enumerable.Empty<int>().Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.Empty<int>().Consume(this.consumer);
    }
}
