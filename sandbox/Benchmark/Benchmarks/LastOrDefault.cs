namespace Benchmark.Benchmarks;

public class LastOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.LastOrDefault(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.LastOrDefault()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).LastOrDefault());
    }
}
