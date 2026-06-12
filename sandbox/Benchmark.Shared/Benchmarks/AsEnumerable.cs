using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

public class AsEnumerable:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        SystemEnumerable.AsEnumerable(src(1)).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).AsEnumerable().Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }
}
