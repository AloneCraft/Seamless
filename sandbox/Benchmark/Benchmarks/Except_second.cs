namespace Benchmark.Benchmarks;

public class Except_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Except(src,src2).Consume(consumer);
    }

    void InterceptCore(){ this.src.Except(this.src2).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Except(src2).Consume(consumer);
    }
}
