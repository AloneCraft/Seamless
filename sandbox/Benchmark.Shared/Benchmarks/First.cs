namespace Seamless.Benchmarks;

public class First:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).First());
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().First());
    }
}
