namespace Benchmark.Benchmarks;

public class Union_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Union(src,src2).Consume(consumer);
    }

    void InterceptCore(){ this.src.Union(this.src2).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Union(src2).Consume(consumer);
    }
}
