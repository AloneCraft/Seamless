namespace Seamless.Benchmarks;

public class CountBy_keySelector():Base(1_600_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).CountBy(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().CountBy(x=>x%1000).Consume(this.consumer);
    }
}
