namespace Seamless.Benchmarks;

public class Aggregate_seed_func():Base(8_000_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).Aggregate(0L,(acc,x)=>acc+x));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().Aggregate(0L,(acc,x)=>acc+x));
}
