namespace Seamless.Benchmarks;

public class FirstOrDefault:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).FirstOrDefault());
    }

    void InterceptCore(){ this.consumer.Consume(src(1).FirstOrDefault()); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().FirstOrDefault());
    }
}
