namespace Benchmark.Benchmarks;

public class CountBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.CountBy(src,x=>x%1000).Consume(consumer);
    }

    void InterceptCore(){ this.src.CountBy(x=>x%1000).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).CountBy(x=>x%1000).Consume(consumer);
    }
}
