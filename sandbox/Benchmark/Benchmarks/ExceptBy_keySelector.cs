namespace Benchmark;

public class ExceptBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ExceptBy(src,src2,x=>x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.ExceptBy(src2,x=>x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().ExceptBy(src2,x=>x).Consume(consumer);
    }
}
