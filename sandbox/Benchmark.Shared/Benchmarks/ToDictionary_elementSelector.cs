namespace Seamless.Benchmarks;

public class ToDictionary_elementSelector():Base(1_600_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).ToDictionary(x=>x,x=>x*2).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
}
