namespace Benchmark;

public class AggregateBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.AggregateBy(src,x=>x%1000,0,(acc,x)=>acc+x).Consume(consumer);
    }

    void InterceptCore(){ this.src.AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(consumer);
    }
}
