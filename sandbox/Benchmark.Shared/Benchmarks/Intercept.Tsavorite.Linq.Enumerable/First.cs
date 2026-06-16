using global::Tsavorite.Linq;

namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

[BenchmarkTestIterations(350)]
public class First():TsavoriteBase(20_000_000){
    #if BENCH_AS_TEST
    private const int iterationCount=1;
    #else
    private const int iterationCount=5_000;
    #endif

    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).First());
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
