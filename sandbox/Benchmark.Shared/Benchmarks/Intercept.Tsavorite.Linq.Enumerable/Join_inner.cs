using global::Tsavorite.Linq;

namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

public class Join_inner():TsavoriteBase(580_000){
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite()=> this.src(1).Join(this.src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer);

    void JoinCore(){ this.src(1).Join(this.src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer); }

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
