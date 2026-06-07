namespace Benchmark.Benchmarks;

public class ToArray:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToArray(src).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.ToArray().Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).ToArray().Consume(consumer);
    }
}
