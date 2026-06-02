namespace Benchmark;

public class FirstOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.FirstOrDefault(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.FirstOrDefault());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().FirstOrDefault());
    }
}
