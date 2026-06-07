namespace Benchmark.Benchmarks;

public class Average:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Average(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Average()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Average());
    }
}
