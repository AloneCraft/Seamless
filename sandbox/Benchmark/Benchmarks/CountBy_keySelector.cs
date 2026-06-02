namespace Benchmark;

public class CountBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.CountBy(src,x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.CountBy(x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().CountBy(x=>x%1000).Consume(consumer);
    }
}
