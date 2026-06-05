namespace Benchmark;

public class SelectMany_indexSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,(x,i)=>x).Consume(consumer);
    }

    void InterceptCore(){ this.srcNested.SelectMany((x,i)=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(srcNested).SelectMany((x,i)=>global::ZLinq.ValueEnumerable.AsValueEnumerable(x)).Consume(consumer);
    }
}
