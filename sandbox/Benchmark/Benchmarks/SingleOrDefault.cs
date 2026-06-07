namespace Benchmark.Benchmarks;

public class SingleOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.SingleOrDefault(srcOne));
    }

    void InterceptCore(){ this.consumer.Consume(this.srcOne.SingleOrDefault()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(srcOne).SingleOrDefault());
    }
}
