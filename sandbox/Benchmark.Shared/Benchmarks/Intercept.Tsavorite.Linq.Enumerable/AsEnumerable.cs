using SystemEnumerable=System.Linq.Enumerable;

using global::Tsavorite.Linq;

namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

public class AsEnumerable():TsavoriteBase(16_030_000){
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite()=>SystemEnumerable.AsEnumerable(this.src(1)).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
