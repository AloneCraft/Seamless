namespace Benchmark.Benchmarks;

public class Single_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.Single(this.srcOne,x=>x==500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.srcOne.Single(x=>x==500000)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.srcOne).Single(x=>x==500000));
    }
}
