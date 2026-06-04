namespace Benchmark;

public class CountBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.CountBy(src,x=>x%1000).Consume(consumer);
    }

    void InterceptCore(){ this.src.CountBy(x=>x%1000).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().CountBy(x=>x%1000).Consume(consumer);
    }
}
