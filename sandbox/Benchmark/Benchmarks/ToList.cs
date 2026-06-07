namespace Benchmark.Benchmarks;

public class ToList:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToList(this.src).Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        this.src.ToList().Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ToList().Consume(this.consumer);
    }
}
