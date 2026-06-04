namespace Benchmark;

public class ThenByDescending_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ThenByDescending(Enumerable.OrderBy(src,x=>x%1000),x=>x).Consume(consumer);
    }

    void InterceptCore(){ this.src.OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(consumer);
    }
}
