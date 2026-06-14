namespace Seamless.Benchmarks;

[BenchmarkTestIterations(18)]
public class ElementAtOrDefault_index():Base(16_340_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).ElementAtOrDefault(500_000));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().ElementAtOrDefault(500_000));
}
