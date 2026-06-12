namespace Seamless.Benchmarks;

public class SingleOrDefault:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(srcOne(1).SingleOrDefault());
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(srcOne(1).AsValueEnumerable().SingleOrDefault());
    }
}
