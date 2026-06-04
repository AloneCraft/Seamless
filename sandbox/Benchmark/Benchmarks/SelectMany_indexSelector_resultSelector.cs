namespace Benchmark;

public class SelectMany_indexSelector_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,(x,i)=>x,(outer,inner)=>inner+1).Consume(consumer);
    }

    void InterceptCore(){ this.srcNested.SelectMany((x,i)=>x,(outer,inner)=>inner+1).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        srcNested.AsValueEnumerable().SelectMany((x,i)=>x.AsValueEnumerable(),(outer,inner)=>inner+1).Consume(consumer);
    }
}
