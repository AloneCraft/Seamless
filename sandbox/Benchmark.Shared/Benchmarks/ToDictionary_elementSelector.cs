namespace Seamless.Benchmarks;

public class ToDictionary_elementSelector():Base(1_600_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
    }
}
