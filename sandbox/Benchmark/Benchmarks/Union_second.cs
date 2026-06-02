namespace Benchmark;

public class Union_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Union(src,src2).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Union(src2).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Union(src2).Consume(consumer);
    }
}
