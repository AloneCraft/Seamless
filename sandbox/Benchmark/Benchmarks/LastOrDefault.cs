namespace Benchmark;

public class LastOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.LastOrDefault(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.LastOrDefault());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().LastOrDefault());
    }
}
