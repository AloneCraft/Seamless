namespace Benchmark.Benchmarks;

public class LongCount:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.LongCount(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.LongCount()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).LongCount());
    }
}
