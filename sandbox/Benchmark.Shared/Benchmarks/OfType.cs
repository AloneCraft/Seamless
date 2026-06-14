namespace Seamless.Benchmarks;

public class OfType():Base(1_800_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).OfType<int>().Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().OfType<int>().Consume(this.consumer);
}
