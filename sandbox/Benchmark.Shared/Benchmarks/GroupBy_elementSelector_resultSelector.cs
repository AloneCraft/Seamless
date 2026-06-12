namespace Seamless.Benchmarks;

public class GroupBy_elementSelector_resultSelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);
    }
}
