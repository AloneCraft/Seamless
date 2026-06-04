namespace Benchmark;

public class GroupBy_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.GroupBy(src,x=>x%1000,(key,g)=>key).Consume(consumer);
    }

    void InterceptCore(){ this.src.GroupBy(x=>x%1000,(key,g)=>key).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().GroupBy(x=>x%1000,(key,g)=>key).Consume(consumer);
    }
}
