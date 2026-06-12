namespace Seamless.Benchmarks;

public class Min_selector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).Min(x=>x*2));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).Min(x=>x*2)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Min(x=>x*2));
    }
}
