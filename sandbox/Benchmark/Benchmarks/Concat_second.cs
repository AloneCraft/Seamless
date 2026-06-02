namespace Benchmark;

public class Concat_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Concat(src,src2).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Concat(src2).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Concat(src2).Consume(consumer);
    }
}
