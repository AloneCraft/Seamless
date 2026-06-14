namespace Seamless.Benchmarks;

[BenchmarkTestIterations(6)]
public class Any_predicate():Base(10_680_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        const int value=999_999;
        this.consumer.Consume(src(1).Any(x=>x>value));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        const int value=999_999;
        this.consumer.Consume(src(1).AsValueEnumerable().Any(x=>x>value));
    }
}
