namespace Benchmark.Benchmarks;

public class Count:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Count(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Count()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Count());
    }
}
