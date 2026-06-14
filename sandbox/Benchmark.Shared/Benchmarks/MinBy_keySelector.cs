namespace Seamless.Benchmarks;

public class MinBy_keySelector():Base(4_500_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).MinBy(x=>x%1000));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().MinBy(x=>x%1000));
}
