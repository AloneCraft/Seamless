namespace Benchmark;

public class LastOrDefault_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.LastOrDefault(src,x=>x>500000));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.LastOrDefault(x=>x>500000));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().LastOrDefault(x=>x>500000));
    }
}
