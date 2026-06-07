namespace Benchmark.Benchmarks;

public class Single:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Single(this.srcOne));
    }

    void InterceptCore(){ this.consumer.Consume(this.srcOne.Single()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.srcOne).Single());
    }
}
