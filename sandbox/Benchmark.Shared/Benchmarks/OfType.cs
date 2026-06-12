namespace Seamless.Benchmarks;

public class OfType:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).OfType<int>().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().OfType<int>().Consume(this.consumer);
    }
}
