namespace Benchmark.Benchmarks;

public class SequenceEqual_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.SequenceEqual(this.src,this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.SequenceEqual(this.src)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).SequenceEqual(this.src));
    }
}
