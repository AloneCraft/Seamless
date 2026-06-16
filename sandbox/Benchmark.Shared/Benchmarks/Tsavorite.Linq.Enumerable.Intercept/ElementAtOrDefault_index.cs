using global::Tsavorite.Linq;

namespace Benchmark.Tsavorite.Linq.Enumerable.Intercept;

[BenchmarkTestIterations(18)]
public class ElementAtOrDefault_index():TsavoriteBase(16_340_000){
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite()=>this.consumer.Consume(this.src(1).ElementAtOrDefault(500_000));

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
