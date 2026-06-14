namespace Seamless.Benchmarks;

public class ToList():Base(3_760_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).ToList().Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().ToList().Consume(this.consumer);
}
