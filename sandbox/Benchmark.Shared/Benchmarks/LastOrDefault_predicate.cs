namespace Seamless.Benchmarks;

public class LastOrDefault_predicate():Base(6_130_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).LastOrDefault(x=>x>500000));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().LastOrDefault(x=>x>500000));
    }
}
