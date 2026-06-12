namespace Seamless.Benchmarks;

public class ToArray:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).ToArray().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void Intercept(){
        src(1).ToArray().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().ToArray().Consume(this.consumer);
    }
}
