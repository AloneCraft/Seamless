namespace Benchmark;

public class Count:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Count(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Count()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Count());
    }
}
