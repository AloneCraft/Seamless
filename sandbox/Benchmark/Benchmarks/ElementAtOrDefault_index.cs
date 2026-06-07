namespace Benchmark.Benchmarks;

public class ElementAtOrDefault_index:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.ElementAtOrDefault(this.src,500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.ElementAtOrDefault(500000)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ElementAtOrDefault(500000));
    }
}
