namespace Benchmark;

public class MinBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.MinBy(src,x=>x%1000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.MinBy(x=>x%1000)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().MinBy(x=>x%1000));
    }
}
