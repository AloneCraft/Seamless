namespace Benchmark.Benchmarks;

public class All_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.All(this.src,x=>x>0));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.All(x=>x>0)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).All(x=>x>0));
    }
}
