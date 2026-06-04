namespace Benchmark;

public class Last:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Last(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Last()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Last());
    }
}
