namespace Benchmark;

public class FirstOrDefault:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.FirstOrDefault(src));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.FirstOrDefault()); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().FirstOrDefault());
    }
}
