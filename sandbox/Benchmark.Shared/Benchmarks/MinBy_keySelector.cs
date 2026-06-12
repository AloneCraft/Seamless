namespace Seamless.Benchmarks;

public class MinBy_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).MinBy(x=>x%1000));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).MinBy(x=>x%1000)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().MinBy(x=>x%1000));
    }
}
