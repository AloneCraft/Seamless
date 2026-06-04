namespace Benchmark;

public class Index:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Index(src).Consume(consumer);
    }

    void InterceptCore(){ this.src.Index().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Index().Consume(consumer);
    }
}
