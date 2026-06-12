namespace Seamless.Benchmarks;

public class ToList:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).ToList().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void Intercept(){
        src(1).ToList().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().ToList().Consume(this.consumer);
    }
}
