namespace Benchmark.Benchmarks;

public class Sum:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Sum(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Sum()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Sum());
    }
}
