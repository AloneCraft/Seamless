namespace Seamless.Benchmarks;

public class All_predicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).All(x=>x>0));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).All(x=>x>0)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().All(x=>x>0));
    }
}
