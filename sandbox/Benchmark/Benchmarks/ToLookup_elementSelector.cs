namespace Benchmark.Benchmarks;

public class ToLookup_elementSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToLookup(src,x=>x%1000,x=>x*2).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.ToLookup(x=>x%1000,x=>x*2).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).ToLookup(x=>x%1000,x=>x*2).Consume(consumer);
    }
}
