namespace Benchmark;

public class Min:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Min(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Min());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Min());
    }
}
