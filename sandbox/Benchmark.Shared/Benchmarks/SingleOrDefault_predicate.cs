namespace Seamless.Benchmarks;

public class SingleOrDefault_predicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).SingleOrDefault(x=>x==count/2));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).SingleOrDefault(x=>x==count/2)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().SingleOrDefault(x=>x==count/2));
    }
}
