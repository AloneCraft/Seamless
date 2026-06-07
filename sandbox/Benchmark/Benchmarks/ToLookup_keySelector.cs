namespace Benchmark.Benchmarks;

public class ToLookup_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToLookup(this.src,x=>x%1000).Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        this.src.ToLookup(x=>x%1000).Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ToLookup(x=>x%1000).Consume(this.consumer);
    }
}
