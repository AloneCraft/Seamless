namespace Seamless.Benchmarks;

public class Count_predicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).Count(x=>(x&1)==0));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).Count(x=>(x&1)==0)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Count(x=>(x&1)==0));
    }
}
