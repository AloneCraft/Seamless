namespace Seamless.Benchmarks;

public class Aggregate_func:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).Aggregate((acc,x)=>acc+x));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).Aggregate((acc,x)=>acc+x)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Aggregate((acc,x)=>acc+x));
    }
}
