namespace Benchmark;

public class Min_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Min(src,x=>x*2));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Min(x=>x*2)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Min(x=>x*2));
    }
}
