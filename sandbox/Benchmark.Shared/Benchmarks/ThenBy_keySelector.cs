namespace Seamless.Benchmarks;

public class ThenBy_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer);
    }
}
