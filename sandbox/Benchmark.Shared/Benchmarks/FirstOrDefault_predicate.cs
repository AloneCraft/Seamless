namespace Seamless.Benchmarks;

[BenchmarkTestIterations(11)]
public class FirstOrDefault_predicate():Base(16_340_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        const int value=500_000;
        this.consumer.Consume(src(1).FirstOrDefault(x=>x>value));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        const int value=500_000;
        this.consumer.Consume(src(1).AsValueEnumerable().FirstOrDefault(x=>x>value));
    }
}
