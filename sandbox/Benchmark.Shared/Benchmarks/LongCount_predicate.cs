namespace Seamless.Benchmarks;

public class LongCount_predicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).LongCount(x=>(x&1)==0));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).LongCount(x=>(x&1)==0)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().LongCount(x=>(x&1)==0));
    }
}
