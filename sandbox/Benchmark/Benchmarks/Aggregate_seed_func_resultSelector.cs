namespace Benchmark.Benchmarks;

public class Aggregate_seed_func_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Aggregate(this.src,0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Aggregate(0L,(acc,x)=>acc+x,acc=>(double)acc/1000000)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Aggregate(0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));
    }
}
