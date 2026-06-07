namespace Benchmark.Benchmarks;

public class FirstOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.FirstOrDefault(this.src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.FirstOrDefault()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).FirstOrDefault());
    }
}
