namespace Benchmark.Benchmarks;

public class Any_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Any(this.src,x=>x>999999));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Any(x=>x>999999)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Any(x=>x>999999));
    }
}
