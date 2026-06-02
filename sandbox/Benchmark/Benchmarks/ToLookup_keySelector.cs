namespace Benchmark;

public class ToLookup_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToLookup(src,x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.ToLookup(x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().ToLookup(x=>x%1000).Consume(consumer);
    }
}
