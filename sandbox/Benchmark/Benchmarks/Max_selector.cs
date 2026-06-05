namespace Benchmark;

public class Max_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Max(src,x=>x*2));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Max(x=>x*2)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Max(x=>x*2));
    }
}
