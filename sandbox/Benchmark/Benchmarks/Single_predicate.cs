namespace Benchmark;

public class Single_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Single(srcOne,x=>x==500000));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(srcOne.Single(x=>x==500000));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(srcOne.AsValueEnumerable().Single(x=>x==500000));
    }
}
