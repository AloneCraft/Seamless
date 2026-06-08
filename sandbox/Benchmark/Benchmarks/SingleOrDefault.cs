namespace Benchmark.Benchmarks;

public class SingleOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.SingleOrDefault(this.srcOne));
    }

    void InterceptCore(){ this.consumer.Consume(this.srcOne.SingleOrDefault()); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.srcOne).SingleOrDefault());
    }
}
