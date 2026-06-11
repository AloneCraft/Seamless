namespace Benchmark.Benchmarks;

public class Contains_value:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Contains(this.src,500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Contains(500000)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Contains(500000));
    }
}
