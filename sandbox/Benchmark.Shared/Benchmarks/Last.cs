namespace Benchmark.Benchmarks;

public class Last:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Last(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Last()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Last());
    }
}
