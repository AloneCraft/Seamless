using SystemEnumerable=System.Linq.Enumerable;

namespace Benchmark.Benchmarks;

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
        ConsumeResult(this.consumer,SystemEnumerable.TryGetNonEnumeratedCount(this.src,out var count),count);
    }

    void InterceptCore(){ ConsumeResult(this.consumer,this.src.TryGetNonEnumeratedCount(out var count),count); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ConsumeResult(this.consumer,ValueEnumerable.AsValueEnumerable(this.src).TryGetNonEnumeratedCount(out var count),count);
    }
}
