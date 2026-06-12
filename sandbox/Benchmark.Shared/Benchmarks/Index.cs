namespace Seamless.Benchmarks;

public class Index:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).Index().Consume(this.consumer);
    }

    void InterceptCore(){ src(1).Index().Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().Index().Consume(this.consumer);
    }
}
