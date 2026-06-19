namespace Benchmark.System.Linq.Enumerable.Intercept;

public class InvariantHoisting():Base(5_720_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
