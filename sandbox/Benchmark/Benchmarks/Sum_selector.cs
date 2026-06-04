namespace Benchmark;

public class Sum_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Sum(src,x=>(long)x));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Sum(x=>(long)x)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Sum(x=>(long)x));
    }
}
