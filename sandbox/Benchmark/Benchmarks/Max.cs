namespace Benchmark.Benchmarks;

public class Max:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Max(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Max()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Max());
    }
}
