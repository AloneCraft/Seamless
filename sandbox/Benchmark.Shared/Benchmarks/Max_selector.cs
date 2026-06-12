namespace Seamless.Benchmarks;

public class Max_selector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).Max(x=>x*2));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).Max(x=>x*2)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Max(x=>x*2));
    }
}
