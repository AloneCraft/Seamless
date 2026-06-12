namespace Seamless.Benchmarks;

public class Sum:Base {
    [BenchFact]
    [Benchmark(Baseline = true)]
    [NoLinqRewrite]
    public void LINQ() {
        this.consumer.Consume(srcMinusPlus(1).Sum());
    }

    void InterceptCore() { this.consumer.Consume(srcMinusPlus(1).Sum()); }

    [BenchFact]
    [Benchmark]
    public void Intercept() { this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq() {
        this.consumer.Consume(srcMinusPlus(1).AsValueEnumerable().Sum());
    }
}
