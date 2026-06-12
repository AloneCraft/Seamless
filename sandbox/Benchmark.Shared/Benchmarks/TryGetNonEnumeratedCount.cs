using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

public class TryGetNonEnumeratedCount:Base{
    static void ConsumeResult(Consumer consumer,bool result,int count){
        consumer.Consume(result);
        if(result){
            consumer.Consume(count);
        }else{
            consumer.Consume(-1);
        }
    }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        ConsumeResult(this.consumer,SystemEnumerable.TryGetNonEnumeratedCount(src(1),out var count),count);
    }

    void InterceptCore(){ ConsumeResult(this.consumer,src(1).TryGetNonEnumeratedCount(out var count),count); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        ConsumeResult(this.consumer,src(1).AsValueEnumerable().TryGetNonEnumeratedCount(out var count),count);
    }
}
