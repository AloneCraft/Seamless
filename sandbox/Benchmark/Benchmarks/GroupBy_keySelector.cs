namespace Benchmark;

public class GroupBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.GroupBy(src,x=>x%1000).Consume(consumer);
    }

    void InterceptCore(){ this.src.GroupBy(x=>x%1000).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().GroupBy(x=>x%1000).Consume(consumer);
    }
}
