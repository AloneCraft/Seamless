namespace Benchmark;

public class ToDictionary_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToDictionary(src,x=>x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.ToDictionary(x=>x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().ToDictionary(x=>x).Consume(consumer);
    }
}
