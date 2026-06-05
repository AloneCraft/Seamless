namespace Benchmark;

public class Average_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Average(src,x=>(double)x));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Average(x=>(double)x)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Average(x=>(double)x));
    }
}
