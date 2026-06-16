using global::Tsavorite.Linq;

namespace Benchmark.Tsavorite.Linq.Enumerable.Intercept;

public class Count_predicate():TsavoriteBase(5_120_000){
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite()=>this.consumer.Consume(this.src(1).Count(x=>(x&1)==0));

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
