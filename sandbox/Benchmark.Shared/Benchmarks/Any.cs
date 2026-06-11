namespace Benchmark.Benchmarks;

public class Any:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Any(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Any()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Any());
    }
}
