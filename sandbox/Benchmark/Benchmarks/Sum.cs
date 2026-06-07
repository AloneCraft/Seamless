namespace Benchmark.Benchmarks;

public class Sum:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Sum(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Sum()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Sum());
    }
}
