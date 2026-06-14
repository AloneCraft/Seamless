namespace Seamless.Benchmarks;

public class Index():Base(3_000_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).Index().Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().Index().Consume(this.consumer);
}
