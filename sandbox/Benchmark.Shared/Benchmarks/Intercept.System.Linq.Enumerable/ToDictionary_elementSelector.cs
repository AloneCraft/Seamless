namespace Benchmark.Intercept.System.Linq.Enumerable;

public class ToDictionary_elementSelector():Base(1_600_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).ToDictionary(x=>x,x=>x*2).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().ToDictionary(x=>x,x=>x*2).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
