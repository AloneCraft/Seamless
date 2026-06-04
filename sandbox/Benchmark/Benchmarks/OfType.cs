namespace Benchmark;

public class OfType:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.OfType<int>(src).Consume(consumer);
    }

    void InterceptCore(){ this.src.OfType<int>().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().OfType<int>().Consume(consumer);
    }
}
