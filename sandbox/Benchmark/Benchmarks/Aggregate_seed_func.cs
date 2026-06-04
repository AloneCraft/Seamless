namespace Benchmark;

public class Aggregate_seed_func:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Aggregate(src,0L,(acc,x)=>acc+x));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Aggregate(0L,(acc,x)=>acc+x)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Aggregate(0L,(acc,x)=>acc+x));
    }
}
