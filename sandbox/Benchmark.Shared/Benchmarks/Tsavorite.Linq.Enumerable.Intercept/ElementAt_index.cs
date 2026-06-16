using global::Tsavorite.Linq;

namespace Benchmark.Tsavorite.Linq.Enumerable.Intercept;

[BenchmarkTestIterations(8)]
public class ElementAt_index():TsavoriteBase(15_430_000){
    #if BENCH_AS_TEST
    private const int iterationCount=1;
    private const int ElementIndex=500;
    #else
    private const int iterationCount=3;
    private const int ElementIndex=500_000;
    #endif

    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).ElementAt(ElementIndex));
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
