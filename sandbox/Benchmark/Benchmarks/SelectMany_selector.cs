namespace Benchmark;

public class SelectMany_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,x=>x).Consume(consumer);
    }

    void InterceptCore(){ this.srcNested.SelectMany(x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        srcNested.AsValueEnumerable().SelectMany(x=>x.AsValueEnumerable()).Consume(consumer);
    }
}
