namespace Benchmark.Benchmarks;

public class Min_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Min(this.src,x=>x*2));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Min(x=>x*2)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Min(x=>x*2));
    }
}
