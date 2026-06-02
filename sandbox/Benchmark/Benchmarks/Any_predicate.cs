namespace Benchmark;

public class Any_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Any(src,x=>x>999999));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Any(x=>x>999999));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Any(x=>x>999999));
    }
}
