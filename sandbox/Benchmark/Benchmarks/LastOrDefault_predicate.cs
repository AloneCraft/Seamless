namespace Benchmark;

public class LastOrDefault_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.LastOrDefault(src,x=>x>500000));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.LastOrDefault(x=>x>500000)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().LastOrDefault(x=>x>500000));
    }
}
