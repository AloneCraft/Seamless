namespace Seamless.Benchmarks;

public class MaxBy_keySelector():Base(6_130_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).MaxBy(x=>x%1000));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().MaxBy(x=>x%1000));
}
