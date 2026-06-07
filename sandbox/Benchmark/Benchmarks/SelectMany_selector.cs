namespace Benchmark.Benchmarks;

public class SelectMany_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,x=>x).Consume(consumer);
    }

    void InterceptCore(){ this.srcNested.SelectMany(x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(srcNested).SelectMany(x=>global::ZLinq.ValueEnumerable.AsValueEnumerable(x)).Consume(consumer);
    }
}
