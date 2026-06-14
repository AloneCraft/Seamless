namespace Seamless.Benchmarks;

public class AsQueryable():Base(14_590_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Queryable.AsQueryable(src(1)).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }
}
