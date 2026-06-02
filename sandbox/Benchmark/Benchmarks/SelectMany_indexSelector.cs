namespace Benchmark;

public class SelectMany_indexSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,(x,i)=>x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        srcNested.SelectMany((x,i)=>x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        srcNested.AsValueEnumerable().SelectMany((x,i)=>x.AsValueEnumerable()).Consume(consumer);
    }
}
