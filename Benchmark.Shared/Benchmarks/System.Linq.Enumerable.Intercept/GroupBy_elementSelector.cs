namespace Benchmark.System.Linq.Enumerable.Intercept;

public class GroupBy_elementSelector():Base(2_800_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).GroupBy(x=>x%1000,x=>x*2).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().GroupBy(x=>x%1000,x=>x*2).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
