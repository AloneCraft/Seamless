namespace Benchmark;

public class Sum_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Sum(src,x=>(long)x));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Sum(x=>(long)x));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Sum(x=>(long)x));
    }
}
