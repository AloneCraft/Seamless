namespace Benchmark.Benchmarks;

public class ElementAt_index:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.ElementAt(this.src,500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.ElementAt(500000)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).ElementAt(500000));
    }
}
