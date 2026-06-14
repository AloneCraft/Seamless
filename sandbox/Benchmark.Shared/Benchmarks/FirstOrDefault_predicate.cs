namespace Seamless.Benchmarks;

[BenchmarkTestIterations(11)]
public class FirstOrDefault_predicate():Base(16_340_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).FirstOrDefault(x=>x>500_000));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().FirstOrDefault(x=>x>500_000));
}
