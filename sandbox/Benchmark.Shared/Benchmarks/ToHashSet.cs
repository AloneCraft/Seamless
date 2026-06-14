namespace Seamless.Benchmarks;

public class ToHashSet():Base(3_000_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).ToHashSet().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().ToHashSet().Consume(this.consumer);
    }
}
