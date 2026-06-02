namespace Benchmark;

public class UnionBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.UnionBy(src,src2,x=>x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.UnionBy(src2,x=>x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().UnionBy(src2,x=>x).Consume(consumer);
    }
}
