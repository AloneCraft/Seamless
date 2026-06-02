namespace Benchmark;

public class GroupBy_elementSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.GroupBy(src,x=>x%1000,x=>x*2).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.GroupBy(x=>x%1000,x=>x*2).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().GroupBy(x=>x%1000,x=>x*2).Consume(consumer);
    }
}
