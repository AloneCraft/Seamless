namespace Seamless.Benchmarks;

public class SingleOrDefault_predicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).SingleOrDefault(x=>x==count/2));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().SingleOrDefault(x=>x==count/2));
    }
}
