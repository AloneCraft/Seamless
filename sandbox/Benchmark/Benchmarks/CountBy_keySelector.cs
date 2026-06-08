namespace Benchmark.Benchmarks;

public class CountBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.CountBy(this.src,x=>x%1000).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.CountBy(x=>x%1000).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).CountBy(x=>x%1000).Consume(this.consumer);
    }
}
