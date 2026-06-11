namespace Benchmark.Benchmarks;

public class Average_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Average(this.src,x=>(double)x));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Average(x=>(double)x)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Average(x=>(double)x));
    }
}
