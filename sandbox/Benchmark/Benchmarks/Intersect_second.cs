namespace Benchmark;

public class Intersect_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Intersect(src,src2).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Intersect(src2).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Intersect(src2).Consume(consumer);
    }
}
