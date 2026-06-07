namespace Benchmark.Benchmarks;

public class Aggregate_seed_func:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Aggregate(this.src,0L,(acc,x)=>acc+x));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Aggregate(0L,(acc,x)=>acc+x)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Aggregate(0L,(acc,x)=>acc+x));
    }
}
