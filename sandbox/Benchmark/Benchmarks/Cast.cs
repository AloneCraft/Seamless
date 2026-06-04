namespace Benchmark;

public class Cast:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Cast<int>(src).Consume(consumer);
    }

    void InterceptCore(){ this.src.Cast<int>().Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Cast<int>().Consume(consumer);
    }
}
