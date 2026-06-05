namespace Benchmark;

public class LastOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.LastOrDefault(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.LastOrDefault()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).LastOrDefault());
    }
}
