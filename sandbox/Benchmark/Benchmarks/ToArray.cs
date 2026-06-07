namespace Benchmark.Benchmarks;

public class ToArray:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToArray(this.src).Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        this.src.ToArray().Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ToArray().Consume(this.consumer);
    }
}
