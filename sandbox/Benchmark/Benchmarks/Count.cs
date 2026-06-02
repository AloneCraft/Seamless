namespace Benchmark;

public class Count:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Count(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Count());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Count());
    }
}
