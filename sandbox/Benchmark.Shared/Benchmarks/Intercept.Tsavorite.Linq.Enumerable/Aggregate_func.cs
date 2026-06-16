using global::Tsavorite.Linq;

namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

public class Aggregate_func():TsavoriteBase(3_000_000){
    [BenchFact,Benchmark(Baseline=true)]
    public void Tsavorite(){
        var result=this.src(1).Aggregate((acc,x)=>acc+x);
        this.Result=result;
        this.consumer.Consume(result);
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
