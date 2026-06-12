namespace Seamless.Benchmarks;

public class AggregateBy_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);
    }
}
