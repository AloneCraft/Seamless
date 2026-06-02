namespace Benchmark;

public class Aggregate_seed_func_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Aggregate(src,0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Aggregate(0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Aggregate(0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));
    }
}
