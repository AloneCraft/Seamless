namespace Seamless.Benchmarks;

public class OfType:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).OfType<int>().Consume(this.consumer);
    }

    void InterceptCore(){ src(1).OfType<int>().Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().OfType<int>().Consume(this.consumer);
    }
}
