namespace Benchmark.Benchmarks;

public class AsQueryable:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        global::System.Linq.Queryable.AsQueryable(this.src).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.AsQueryable().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }
}
