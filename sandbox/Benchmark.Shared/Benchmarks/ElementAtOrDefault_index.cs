namespace Seamless.Benchmarks;

[BenchmarkTestIterations(18)]
public class ElementAtOrDefault_index():Base(16_340_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        const int index=500_000;
        this.consumer.Consume(src(1).ElementAtOrDefault(index));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        const int index=500_000;
        this.consumer.Consume(src(1).AsValueEnumerable().ElementAtOrDefault(index));
    }
}
