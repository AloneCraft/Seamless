namespace Benchmark.Intercept.System.Linq.Enumerable;

public class Join_innerKey_Hoisting:Base{
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).Join(this.src2(1),o=>o%1000,i=>(i+HoistInvariant.Value())%1000,(o,i)=>o+i).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().Join(this.src2(1),o => o%1000,i => (i+HoistInvariant.Value())%1000,(o,i)=>o+i).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
