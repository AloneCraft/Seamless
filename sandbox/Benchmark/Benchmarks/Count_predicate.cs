namespace Benchmark;

public class Count_predicate:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Count(src,x=>(x&1)==0));
    }

    void InterceptCore(){ this.consumer.Consume(this.src.Count(x=>(x&1)==0)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(src.AsValueEnumerable().Count(x=>(x&1)==0));
    }
}
