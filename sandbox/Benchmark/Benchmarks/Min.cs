namespace Benchmark.Benchmarks;

public class Min:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Min(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Min()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Min());
    }
}
