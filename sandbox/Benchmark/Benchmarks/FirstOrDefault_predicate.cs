namespace Benchmark;

public class FirstOrDefault_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.FirstOrDefault(src,x=>x>500000));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.FirstOrDefault(x=>x>500000));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().FirstOrDefault(x=>x>500000));
    }
}
