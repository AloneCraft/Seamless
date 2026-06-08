namespace Benchmark.Benchmarks;

public class Count_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Count(this.src,x=>(x&1)==0));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Count(x=>(x&1)==0)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).Count(x=>(x&1)==0));
    }
}
