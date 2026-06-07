namespace Benchmark.Benchmarks;

public class LongCount:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.LongCount(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.LongCount()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).LongCount());
    }
}
