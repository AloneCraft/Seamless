namespace Benchmark;

public class AsQueryable:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        global::System.Linq.Queryable.AsQueryable(src).Consume(consumer);
    }

    void InterceptCore(){ this.src.AsQueryable().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }
}
