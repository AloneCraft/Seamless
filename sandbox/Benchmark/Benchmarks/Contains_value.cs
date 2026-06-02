namespace Benchmark;

public class Contains_value:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Contains(src,500000));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Contains(500000));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Contains(500000));
    }
}
