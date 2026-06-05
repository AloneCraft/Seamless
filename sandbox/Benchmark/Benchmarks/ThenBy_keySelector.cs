namespace Benchmark;

public class ThenBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ThenBy(Enumerable.OrderBy(src,x=>x%1000),x=>x).Consume(consumer);
    }

    void InterceptCore(){ this.src.OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).OrderBy(x=>x%1000).ThenBy(x=>x).Consume(consumer);
    }
}
