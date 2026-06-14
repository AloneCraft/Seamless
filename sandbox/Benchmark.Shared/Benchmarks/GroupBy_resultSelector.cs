namespace Seamless.Benchmarks;

public class GroupBy_resultSelector():Base(1_600_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).GroupBy(x=>x%1000,(key,g)=>key).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().GroupBy(x=>x%1000,(key,g)=>key).Consume(this.consumer);
    }
}
