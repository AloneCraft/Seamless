namespace Seamless.Benchmarks;

public class Max:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).Max());
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Max());
    }
}
