namespace Seamless.Benchmarks;

public class Sum_selector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).Sum(x=>(long)x));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).Sum(x=>(long)x)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Sum(x=>(long)x));
    }
}
