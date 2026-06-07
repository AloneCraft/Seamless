namespace Benchmark.Benchmarks;

public class MinBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.MinBy(this.src,x=>x%1000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.MinBy(x=>x%1000)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).MinBy(x=>x%1000));
    }
}
