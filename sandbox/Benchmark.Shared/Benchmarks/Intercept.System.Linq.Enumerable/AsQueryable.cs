namespace Benchmark.Intercept.System.Linq.Enumerable;

public class AsQueryable():Base(14_590_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>Queryable.AsQueryable(this.src(1)).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
