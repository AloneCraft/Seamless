namespace Seamless.Benchmarks;

public class AsQueryable:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Queryable.AsQueryable(src(1)).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }
}
