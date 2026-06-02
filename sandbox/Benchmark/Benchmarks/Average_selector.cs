namespace Benchmark;

public class Average_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Average(src,x=>(double)x));
    }

    [Benchmark]
    public void Intercept(){
        consumer.Consume(src.Average(x=>(double)x));
    }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Average(x=>(double)x));
    }
}
