namespace Seamless.Benchmarks;

public class InvariantHoisting():Base(5_720_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);
}
