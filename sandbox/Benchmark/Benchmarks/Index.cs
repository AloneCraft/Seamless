namespace Benchmark;

public class Index:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Index(src).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Index().Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Index().Consume(consumer);
    }
}
