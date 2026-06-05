namespace Benchmark;

public class UnionBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.UnionBy(src,src2,x=>x).Consume(consumer);
    }

    void InterceptCore(){ this.src.UnionBy(this.src2,x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).UnionBy(src2,x=>x).Consume(consumer);
    }
}
