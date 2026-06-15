namespace Seamless.Benchmarks;

public class ThenByDescending_keySelector():Base(240_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
