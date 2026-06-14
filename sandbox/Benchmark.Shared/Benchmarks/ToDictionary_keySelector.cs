namespace Seamless.Benchmarks;

public class ToDictionary_keySelector():Base(1_800_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).ToDictionary(x=>x).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().ToDictionary(x=>x).Consume(this.consumer);
}
