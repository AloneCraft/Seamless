namespace Benchmark;

public class Concat_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Concat(src,src2).Consume(consumer);
    }

    void InterceptCore(){ this.src.Concat(this.src2).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Concat(src2).Consume(consumer);
    }
}
