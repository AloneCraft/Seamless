namespace Seamless.Benchmarks;

public class ToDictionary_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).ToDictionary(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().ToDictionary(x=>x).Consume(this.consumer);
    }
}
