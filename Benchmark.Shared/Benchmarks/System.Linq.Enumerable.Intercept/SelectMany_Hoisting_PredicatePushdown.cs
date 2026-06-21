namespace Benchmark.System.Linq.Enumerable.Intercept;

public class SelectMany_Hoisting_PredicatePushdown:Base{
    private readonly int[] outer=global::System.Linq.Enumerable.Range(0,3_000).ToArray();
    private readonly int[] inner=global::System.Linq.Enumerable.Range(0,1_280).ToArray();
    private const int Threshold=120;

    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.outer.SelectMany(_=>this.inner).Where(x=>x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.outer.AsValueEnumerable().SelectMany(_=>this.inner.AsValueEnumerable()).Where(x=>x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
