namespace Benchmark.Benchmarks;

public class FirstOrDefault_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.FirstOrDefault(src,x=>x>500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.FirstOrDefault(x=>x>500000)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).FirstOrDefault(x=>x>500000));
    }
}
