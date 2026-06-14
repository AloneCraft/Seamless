namespace Seamless.Benchmarks;

public class Zip_tuple():Base(3_790_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).Zip(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().Zip(this.src2(1)).Consume(this.consumer);
}
