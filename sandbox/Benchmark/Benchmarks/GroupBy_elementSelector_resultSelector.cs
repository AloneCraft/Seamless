namespace Benchmark.Benchmarks;

public class GroupBy_elementSelector_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.GroupBy(src,x=>x%1000,x=>x*2,(key,g)=>key).Consume(consumer);
    }

    void InterceptCore(){ this.src.GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(consumer);
    }
}
