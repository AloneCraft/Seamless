namespace Benchmark;

public class ElementAt_index:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.ElementAt(src,500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.ElementAt(500000)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).ElementAt(500000));
    }
}
