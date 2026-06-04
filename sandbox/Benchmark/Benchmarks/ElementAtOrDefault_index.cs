namespace Benchmark;

public class ElementAtOrDefault_index:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.ElementAtOrDefault(src,500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.ElementAtOrDefault(500000)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().ElementAtOrDefault(500000));
    }
}
