namespace Benchmark;

public class SingleOrDefault_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.SingleOrDefault(srcOne,x=>x==500000));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(srcOne.SingleOrDefault(x=>x==500000));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(srcOne.AsValueEnumerable().SingleOrDefault(x=>x==500000));
    }
}
