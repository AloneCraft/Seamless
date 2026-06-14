namespace Seamless.Benchmarks;

public class Sum():Base(5_300_000){
    [BenchFact,Benchmark(Baseline = true)]public void Linq()=>this.consumer.Consume(this.srcMinusPlus(1).Sum());

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.srcMinusPlus(1).AsValueEnumerable().Sum());
}
