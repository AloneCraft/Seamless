namespace Benchmark.Benchmarks;

public class Max_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Max(this.src,x=>x*2));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Max(x=>x*2)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Max(x=>x*2));
    }
}
