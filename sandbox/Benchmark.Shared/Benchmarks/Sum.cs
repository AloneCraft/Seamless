namespace Seamless.Benchmarks;

public class Sum:Base {
    [BenchFact]
    [Benchmark(Baseline = true)]
    public void LINQ() {
        this.consumer.Consume(srcMinusPlus(1).Sum());
    }

    [BenchFact]
    [Benchmark]
    public void FLinq() { this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq() {
        this.consumer.Consume(srcMinusPlus(1).AsValueEnumerable().Sum());
    }
}
