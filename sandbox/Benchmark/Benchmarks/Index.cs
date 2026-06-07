namespace Benchmark.Benchmarks;

public class Index:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Index(this.src).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.Index().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Index().Consume(this.consumer);
    }
}
