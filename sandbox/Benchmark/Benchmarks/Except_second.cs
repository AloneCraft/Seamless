namespace Benchmark;

public class Except_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Except(src,src2).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Except(src2).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Except(src2).Consume(consumer);
    }
}
