using global::Tsavorite.Linq;

namespace Benchmark.Tsavorite.Linq.Enumerable.Intercept;

public class ToDictionary_keySelector():TsavoriteBase(1_800_000){
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite()=> this.src(1).ToDictionary(x=>x).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
