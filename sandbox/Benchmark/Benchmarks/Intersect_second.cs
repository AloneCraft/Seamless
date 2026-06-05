namespace Benchmark;

public class Intersect_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Intersect(src,src2).Consume(consumer);
    }

    void InterceptCore(){ this.src.Intersect(this.src2).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Intersect(src2).Consume(consumer);
    }
}
