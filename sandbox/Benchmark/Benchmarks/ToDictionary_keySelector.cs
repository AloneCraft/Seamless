namespace Benchmark.Benchmarks;

public class ToDictionary_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToDictionary(this.src,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        this.src.ToDictionary(x=>x).Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ToDictionary(x=>x).Consume(this.consumer);
    }
}
