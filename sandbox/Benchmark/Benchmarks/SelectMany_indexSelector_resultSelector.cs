namespace Benchmark;

public class SelectMany_indexSelector_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,(x,i)=>x,(outer,inner)=>inner+1).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        srcNested.SelectMany((x,i)=>x,(outer,inner)=>inner+1).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        srcNested.AsValueEnumerable().SelectMany((x,i)=>x.AsValueEnumerable(),(outer,inner)=>inner+1).Consume(consumer);
    }
}
