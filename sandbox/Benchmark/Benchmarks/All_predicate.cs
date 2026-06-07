namespace Benchmark.Benchmarks;

public class All_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.All(src,x=>x>0));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.All(x=>x>0)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).All(x=>x>0));
    }
}
