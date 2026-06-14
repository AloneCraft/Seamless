namespace Seamless.Benchmarks;

public class SequenceEqual_second():Base(1_600_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).SequenceEqual(src(1)));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().SequenceEqual(src(1)));
    }
}
