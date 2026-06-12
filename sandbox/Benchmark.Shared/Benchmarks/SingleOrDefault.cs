namespace Seamless.Benchmarks;

public class SingleOrDefault:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(srcOne(1).SingleOrDefault());
    }

    void InterceptCore(){ this.consumer.Consume(srcOne(1).SingleOrDefault()); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(srcOne(1).AsValueEnumerable().SingleOrDefault());
    }
}
