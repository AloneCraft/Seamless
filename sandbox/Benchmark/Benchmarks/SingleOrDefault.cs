namespace Benchmark;

public class SingleOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.SingleOrDefault(srcOne));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(srcOne.SingleOrDefault());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(srcOne.AsValueEnumerable().SingleOrDefault());
    }
}
