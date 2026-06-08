namespace Benchmark.Benchmarks;

public class LongCount_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.LongCount(this.src,x=>(x&1)==0));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.LongCount(x=>(x&1)==0)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).LongCount(x=>(x&1)==0));
    }
}
