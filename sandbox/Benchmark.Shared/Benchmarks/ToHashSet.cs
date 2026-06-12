namespace Seamless.Benchmarks;

public class ToHashSet:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).ToHashSet().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void Intercept(){
        src(1).ToHashSet().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().ToHashSet().Consume(this.consumer);
    }
}
