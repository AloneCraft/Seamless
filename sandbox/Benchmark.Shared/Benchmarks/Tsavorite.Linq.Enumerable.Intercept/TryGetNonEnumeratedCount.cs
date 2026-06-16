using SystemEnumerable=System.Linq.Enumerable;

using global::Tsavorite.Linq;

namespace Benchmark.Tsavorite.Linq.Enumerable.Intercept;

[BenchmarkTestIterations(800)]
public class TryGetNonEnumeratedCount():TsavoriteBase(20_000_000){
    #if BENCH_AS_TEST
    private const int iterationCount=1;
    #else
    private const int iterationCount=2_000;
    #endif

    static void ConsumeResult(global::Benchmark.Shared.Consumer consumer,bool result,int count){
        consumer.Consume(result);
        if(result){
            consumer.Consume(count);
        }else{
            consumer.Consume(-1);
        }
    }

    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite(){
        for(var i=0;i<iterationCount;i++){
            ConsumeResult(this.consumer,SystemEnumerable.TryGetNonEnumeratedCount(this.src(1),out var count),count);
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
