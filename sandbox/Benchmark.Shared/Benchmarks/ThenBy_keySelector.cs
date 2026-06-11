namespace Benchmark.Benchmarks;

public class ThenBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ThenBy(Enumerable.OrderBy(this.src,x=>x%1000),x=>x).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer);
    }
}
