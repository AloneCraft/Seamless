namespace Benchmark.Benchmarks;

public class ThenByDescending_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ThenByDescending(Enumerable.OrderBy(this.src,x=>x%1000),x=>x).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(this.consumer);
    }
}
