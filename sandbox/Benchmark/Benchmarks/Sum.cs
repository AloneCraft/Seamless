namespace Benchmark;

public class Sum:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Sum(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Sum());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Sum());
    }
}
