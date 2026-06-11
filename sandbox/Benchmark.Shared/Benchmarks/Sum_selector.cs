namespace Benchmark.Benchmarks;

public class Sum_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Sum(this.src,x=>(long)x));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Sum(x=>(long)x)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Sum(x=>(long)x));
    }
}
