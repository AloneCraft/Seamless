namespace Seamless.Benchmarks;

public class MaxBy_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).MaxBy(x=>x%1000));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).MaxBy(x=>x%1000)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().MaxBy(x=>x%1000));
    }
}
