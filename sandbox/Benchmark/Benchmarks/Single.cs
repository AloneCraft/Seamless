namespace Benchmark;

public class Single:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Single(srcOne));
    }

    void InterceptCore(){ this.consumer.Consume(this.srcOne.Single()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(srcOne.AsValueEnumerable().Single());
    }
}
