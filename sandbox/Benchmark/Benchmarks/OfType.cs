namespace Benchmark.Benchmarks;

public class OfType:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.OfType<int>(this.src).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.OfType<int>().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OfType<int>().Consume(this.consumer);
    }
}
