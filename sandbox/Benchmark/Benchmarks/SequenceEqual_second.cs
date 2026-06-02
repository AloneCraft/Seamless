namespace Benchmark;

public class SequenceEqual_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.SequenceEqual(src,src));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.SequenceEqual(src));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().SequenceEqual(src));
    }
}
