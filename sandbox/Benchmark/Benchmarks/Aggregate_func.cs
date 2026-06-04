namespace Benchmark;

public class Aggregate_func:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Aggregate(src,(acc,x)=>acc+x));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Aggregate((acc,x)=>acc+x)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Aggregate((acc,x)=>acc+x));
    }
}
