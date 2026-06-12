namespace Seamless.Benchmarks;

public class AsQueryable:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        Queryable.AsQueryable(src(1)).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).AsQueryable().Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }
}
