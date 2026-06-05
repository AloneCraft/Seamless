using SystemEnumerable=global::System.Linq.Enumerable;

namespace Benchmark;

public class AsEnumerable:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.AsEnumerable(src).Consume(consumer);
    }

    void InterceptCore(){ this.src.AsEnumerable().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }
}
