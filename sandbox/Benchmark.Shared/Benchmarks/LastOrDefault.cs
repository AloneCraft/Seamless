namespace Seamless.Benchmarks;

public class LastOrDefault:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).LastOrDefault());
    }

    void InterceptCore(){ this.consumer.Consume(src(1).LastOrDefault()); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().LastOrDefault());
    }
}
