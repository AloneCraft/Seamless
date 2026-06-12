namespace Seamless.Benchmarks;

public class ThenBy_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer);
    }
}
