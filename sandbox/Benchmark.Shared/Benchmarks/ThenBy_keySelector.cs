namespace Seamless.Benchmarks;

public class ThenBy_keySelector():Base(190_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer);
}
