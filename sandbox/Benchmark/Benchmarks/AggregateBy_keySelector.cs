namespace Benchmark.Benchmarks;

public class AggregateBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.AggregateBy(this.src,x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);
    }
}
