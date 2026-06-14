namespace Seamless.Benchmarks;

public class LastOrDefault_predicate():Base(6_130_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).LastOrDefault(x=>x>500000));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().LastOrDefault(x=>x>500000));
}
