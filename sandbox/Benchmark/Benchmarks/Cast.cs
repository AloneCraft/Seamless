namespace Benchmark.Benchmarks;

public class Cast:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Cast<int>(src).Consume(consumer);
    }

    void InterceptCore(){ this.src.Cast<int>().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Cast<int>().Consume(consumer);
    }
}
