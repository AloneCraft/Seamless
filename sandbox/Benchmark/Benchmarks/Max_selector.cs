namespace Benchmark;

public class Max_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Max(src,x=>x*2));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Max(x=>x*2));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Max(x=>x*2));
    }
}
