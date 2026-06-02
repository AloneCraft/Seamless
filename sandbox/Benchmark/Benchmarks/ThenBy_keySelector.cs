namespace Benchmark;

public class ThenBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ThenBy(Enumerable.OrderBy(src,x=>x%1000),x=>x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.OrderBy(x=>x%1000).ThenBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().OrderBy(x=>x%1000).ThenBy(x=>x).Consume(consumer);
    }
}
