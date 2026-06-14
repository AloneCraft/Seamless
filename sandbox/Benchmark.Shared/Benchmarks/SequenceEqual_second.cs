namespace Seamless.Benchmarks;

public class SequenceEqual_second():Base(1_600_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).SequenceEqual(this.src(1)));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().SequenceEqual(this.src(1)));
}
