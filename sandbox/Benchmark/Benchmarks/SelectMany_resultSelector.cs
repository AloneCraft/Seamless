namespace Benchmark;

public class SelectMany_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,x=>x,(outer,inner)=>inner+1).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        srcNested.SelectMany(x=>x,(outer,inner)=>inner+1).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        srcNested.AsValueEnumerable().SelectMany(x=>x.AsValueEnumerable(),(outer,inner)=>inner+1).Consume(consumer);
    }
}
