namespace Seamless.Benchmarks;

public class Average_selector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).Average(x=>(double)x));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).Average(x=>(double)x)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Average(x=>(double)x));
    }
}
