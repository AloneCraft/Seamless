namespace Benchmark;

public class AggregateBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.AggregateBy(src,x=>x%1000,0,(acc,x)=>acc+x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(consumer);
    }
}
