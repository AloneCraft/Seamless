namespace Seamless.Benchmarks;

public class Any_predicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).Any(x=>x>999999));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).Any(x=>x>999999)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Any(x=>x>999999));
    }
}
