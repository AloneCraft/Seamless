namespace Benchmark.Benchmarks;

public class Contains_value:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Contains(src,500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Contains(500000)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Contains(500000));
    }
}
