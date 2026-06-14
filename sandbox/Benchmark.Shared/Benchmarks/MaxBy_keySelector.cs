namespace Seamless.Benchmarks;

public class MaxBy_keySelector():Base(6_130_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).MaxBy(x=>x%1000));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().MaxBy(x=>x%1000));
    }
}
