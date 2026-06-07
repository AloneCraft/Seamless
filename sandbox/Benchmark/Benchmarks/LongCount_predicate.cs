namespace Benchmark.Benchmarks;

public class LongCount_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.LongCount(src,x=>(x&1)==0));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.LongCount(x=>(x&1)==0)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).LongCount(x=>(x&1)==0));
    }
}
