namespace Seamless.Benchmarks;

public class SelectMany_Hoisting_PredicatePushdown:Base{
    private readonly int[] outer=Enumerable.Range(0,30_000).ToArray();
    private readonly int[] inner=Enumerable.Range(0,128).ToArray();
    private const int Threshold=120;

    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.outer.SelectMany(_=>this.inner).Where(x=>x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.outer.AsValueEnumerable().SelectMany(_=>this.inner.AsValueEnumerable()).Where(x=>x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer);
    }
}
