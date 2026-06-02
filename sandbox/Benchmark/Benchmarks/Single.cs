namespace Benchmark;

public class Single:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Single(srcOne));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(srcOne.Single());
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(srcOne.AsValueEnumerable().Single());
    }
}
