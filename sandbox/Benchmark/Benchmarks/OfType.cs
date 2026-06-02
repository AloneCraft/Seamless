namespace Benchmark;

public class OfType:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.OfType<int>(src).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.OfType<int>().Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().OfType<int>().Consume(consumer);
    }
}
