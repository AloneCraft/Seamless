namespace Benchmark.Benchmarks;

public class SingleOrDefault_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.SingleOrDefault(srcOne,x=>x==500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.srcOne.SingleOrDefault(x=>x==500000)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(srcOne).SingleOrDefault(x=>x==500000));
    }
}
