namespace Benchmark;

public class IntersectBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.IntersectBy(src,src2,x=>x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.IntersectBy(src2,x=>x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().IntersectBy(src2,x=>x).Consume(consumer);
    }
}
