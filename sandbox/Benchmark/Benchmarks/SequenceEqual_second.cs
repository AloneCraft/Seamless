namespace Benchmark;

public class SequenceEqual_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.SequenceEqual(src,src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.SequenceEqual(this.src)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().SequenceEqual(src));
    }
}
