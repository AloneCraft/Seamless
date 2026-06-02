namespace Benchmark;

public class ThenByDescending_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ThenByDescending(Enumerable.OrderBy(src,x=>x%1000),x=>x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(consumer);
    }
}
