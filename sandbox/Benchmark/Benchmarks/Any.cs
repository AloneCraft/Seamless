namespace Benchmark;

public class Any:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Any(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Any());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Any());
    }
}
