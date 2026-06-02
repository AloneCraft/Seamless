namespace Benchmark;

public class Last:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Last(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Last());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Last());
    }
}
