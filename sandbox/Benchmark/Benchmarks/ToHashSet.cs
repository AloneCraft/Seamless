namespace Benchmark.Benchmarks;

public class ToHashSet:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToHashSet(src).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.ToHashSet().Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).ToHashSet().Consume(consumer);
    }
}
