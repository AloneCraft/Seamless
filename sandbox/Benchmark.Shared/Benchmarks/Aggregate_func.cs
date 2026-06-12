namespace Seamless.Benchmarks;

public class Aggregate_func:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).Aggregate((acc,x)=>acc+x));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Aggregate((acc,x)=>acc+x));
    }
}
