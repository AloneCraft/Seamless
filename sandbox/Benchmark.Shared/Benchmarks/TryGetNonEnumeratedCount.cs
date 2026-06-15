using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

[BenchmarkTestIterations(800)]
public class TryGetNonEnumeratedCount():Base(20_000_000){
    private const int iterationCount=2_000;

    static void ConsumeResult(Consumer consumer,bool result,int count){
        consumer.Consume(result);
        if(result){
            consumer.Consume(count);
        }else{
            consumer.Consume(-1);
        }
    }

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        for(var i=0;i<iterationCount;i++){
            ConsumeResult(this.consumer,SystemEnumerable.TryGetNonEnumeratedCount(this.src(1),out var count),count);
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            ConsumeResult(this.consumer,this.src(1).AsValueEnumerable().TryGetNonEnumeratedCount(out var count),count);
        }
    }

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
