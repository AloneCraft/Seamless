namespace Benchmark.System.Linq.Enumerable.Intercept;

public class SelectManyPredicatePushdown:Base{
    private readonly int[] outer=global::System.Linq.Enumerable.Range(0,30_000).ToArray();
    private readonly int[] inner=global::System.Linq.Enumerable.Range(0,128).ToArray();
    private const int Threshold=120;

    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.outer.SelectMany(_=>this.inner).Where(x=>x>Threshold).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.outer.AsValueEnumerable().SelectMany(_=>this.inner.AsValueEnumerable()).Where(x=>x>Threshold).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
