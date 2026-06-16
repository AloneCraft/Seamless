using global::Tsavorite.Linq;

namespace Benchmark.Tsavorite.Linq.Enumerable.Intercept;

public class Aggregate_seed_func_resultSelector():TsavoriteBase(7_150_000){
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite()=>this.consumer.Consume(this.src(1).Aggregate(0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
