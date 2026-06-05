namespace Benchmark;

public class Any_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Any(src,x=>x>999999));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Any(x=>x>999999)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Any(x=>x>999999));
    }
}
