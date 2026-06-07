namespace Benchmark.Benchmarks;

public class SelectMany_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(this.srcNested,x=>x).Consume(this.consumer);
    }

    void InterceptCore(){ this.srcNested.SelectMany(x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.srcNested).SelectMany(x=>global::ZLinq.ValueEnumerable.AsValueEnumerable(x)).Consume(this.consumer);
    }
}
