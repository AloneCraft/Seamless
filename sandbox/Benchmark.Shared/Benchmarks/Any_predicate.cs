namespace Seamless.Benchmarks;

[BenchmarkTestIterations(6)]
public class Any_predicate():Base(10_680_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).Any(x=>x>999_999));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().Any(x=>x>999_999));
}
