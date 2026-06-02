namespace Benchmark;

public class First:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.First(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.First());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().First());
    }
}
