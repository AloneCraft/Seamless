namespace Benchmark;

public class Average:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Average(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Average());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Average());
    }
}
