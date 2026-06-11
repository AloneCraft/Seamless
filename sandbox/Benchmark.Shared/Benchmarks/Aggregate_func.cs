namespace Benchmark.Benchmarks;

public class Aggregate_func:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Aggregate(this.src,(acc,x)=>acc+x));
    }

    public void InterceptCore(){ this.consumer.Consume(this.src.Aggregate((acc,x)=>acc+x)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Aggregate((acc,x)=>acc+x));
    }
}
