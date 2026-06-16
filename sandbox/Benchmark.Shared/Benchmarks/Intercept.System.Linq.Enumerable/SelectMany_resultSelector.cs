namespace Benchmark.Intercept.System.Linq.Enumerable;

public class SelectMany_resultSelector():Base(2_000_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.srcNested(1).SelectMany(x=>x,(outer,inner)=>inner+1).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.srcNested(1).AsValueEnumerable().SelectMany(x=>x.AsValueEnumerable(),(outer,inner)=>inner+1).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
