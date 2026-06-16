namespace Benchmark.Intercept.System.Linq.Enumerable;

public class GroupBy_elementSelector_resultSelector():Base(1_600_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
