namespace Benchmark.Benchmarks;

public class ToHashSet:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToHashSet(this.src).Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        this.src.ToHashSet().Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ToHashSet().Consume(this.consumer);
    }
}
