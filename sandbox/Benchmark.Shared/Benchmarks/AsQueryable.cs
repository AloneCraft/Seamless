namespace Seamless.Benchmarks;

public class AsQueryable():Base(14_590_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>Queryable.AsQueryable(this.src(1)).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();
}
