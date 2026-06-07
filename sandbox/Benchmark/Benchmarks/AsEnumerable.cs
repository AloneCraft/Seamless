using SystemEnumerable=global::System.Linq.Enumerable;

namespace Benchmark.Benchmarks;

public class AsEnumerable:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.AsEnumerable(this.src).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.AsEnumerable().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }
}
