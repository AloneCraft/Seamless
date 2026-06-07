namespace Benchmark.Benchmarks;

public class IntersectBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.IntersectBy(src,src2,x=>x).Consume(consumer);
    }

    void InterceptCore(){ this.src.IntersectBy(this.src2,x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).IntersectBy(src2,x=>x).Consume(consumer);
    }
}
