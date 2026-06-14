namespace Seamless.Benchmarks;

public class CountBy_keySelector():Base(1_600_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).CountBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().CountBy(x=>x%1000).Consume(this.consumer);
}
