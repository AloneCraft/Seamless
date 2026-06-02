namespace Benchmark;

public class Aggregate_func:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Aggregate(src,(acc,x)=>acc+x));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Aggregate((acc,x)=>acc+x));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Aggregate((acc,x)=>acc+x));
    }
}
