namespace Benchmark.Benchmarks;

public class GroupBy_elementSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.GroupBy(this.src,x=>x%1000,x=>x*2).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.GroupBy(x=>x%1000,x=>x*2).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).GroupBy(x=>x%1000,x=>x*2).Consume(this.consumer);
    }
}
