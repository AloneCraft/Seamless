namespace Benchmark.Benchmarks;

public class First:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.First(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.First()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).First());
    }
}
