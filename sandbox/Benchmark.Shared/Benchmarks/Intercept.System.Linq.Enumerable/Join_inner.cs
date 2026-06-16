namespace Benchmark.Intercept.System.Linq.Enumerable;

public class Join_inner():Base(580_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).Join(this.src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer);

    void JoinCore(){ this.src(1).Join(this.src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer); }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().Join(this.src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
