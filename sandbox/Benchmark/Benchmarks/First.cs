namespace Benchmark.Benchmarks;

public class First:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.First(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.First()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).First());
    }
}
