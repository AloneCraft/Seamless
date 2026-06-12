namespace Seamless.Benchmarks;

public class ToLookup_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).ToLookup(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().ToLookup(x=>x%1000).Consume(this.consumer);
    }
}
