namespace Benchmark;

public class ToList:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToList(src).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.ToList().Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().ToList().Consume(consumer);
    }
}
