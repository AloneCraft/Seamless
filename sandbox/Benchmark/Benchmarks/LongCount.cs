namespace Benchmark;

public class LongCount:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.LongCount(src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.LongCount());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().LongCount());
    }
}
