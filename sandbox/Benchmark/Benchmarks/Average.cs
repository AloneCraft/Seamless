namespace Benchmark.Benchmarks;

public class Average:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Average(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Average()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Average());
    }
}
