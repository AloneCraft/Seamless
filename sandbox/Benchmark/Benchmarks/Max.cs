namespace Benchmark.Benchmarks;

public class Max:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Max(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Max()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Max());
    }
}
