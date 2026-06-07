namespace Benchmark.Benchmarks;

public class Min:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Min(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Min()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Min());
    }
}
