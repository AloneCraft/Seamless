namespace Benchmark.Benchmarks;

public class MaxBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.MaxBy(src,x=>x%1000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.MaxBy(x=>x%1000)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).MaxBy(x=>x%1000));
    }
}
