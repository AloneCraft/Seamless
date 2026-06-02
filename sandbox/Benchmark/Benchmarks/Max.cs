namespace Benchmark;

public class Max:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Max(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Max());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Max());
    }
}
