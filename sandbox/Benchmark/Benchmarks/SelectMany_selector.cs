namespace Benchmark;

public class SelectMany_selector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,x=>x).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        srcNested.SelectMany(x=>x).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        srcNested.AsValueEnumerable().SelectMany(x=>x.AsValueEnumerable()).Consume(consumer);
    }
}
