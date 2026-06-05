using SystemEnumerable=global::System.Linq.Enumerable;

namespace Benchmark;

public class TryGetNonEnumeratedCount:Base{
    static void ConsumeResult(Consumer consumer,bool result,int count){
        consumer.Consume(result);
        if(result){
            consumer.Consume(count);
        }else{
            consumer.Consume(-1);
        }
    }

    [Benchmark(Baseline=true)]
    public void LINQ(){
        ConsumeResult(consumer,SystemEnumerable.TryGetNonEnumeratedCount(src,out var count),count);
    }

    void InterceptCore(){ ConsumeResult(this.consumer,this.src.TryGetNonEnumeratedCount(out var count),count); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ConsumeResult(consumer,global::ZLinq.ValueEnumerable.AsValueEnumerable(src).TryGetNonEnumeratedCount(out var count),count);
    }
}
