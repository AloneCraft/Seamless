namespace Benchmark.Benchmarks;

public class FirstOrDefault_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(Enumerable.FirstOrDefault(this.src,x=>x>500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.FirstOrDefault(x=>x>500000)); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(ValueEnumerable.AsValueEnumerable(this.src).FirstOrDefault(x=>x>500000));
    }
}
