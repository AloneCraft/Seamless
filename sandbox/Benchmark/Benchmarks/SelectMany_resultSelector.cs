namespace Benchmark.Benchmarks;

public class SelectMany_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(srcNested,x=>x,(outer,inner)=>inner+1).Consume(consumer);
    }

    void InterceptCore(){ this.srcNested.SelectMany(x=>x,(outer,inner)=>inner+1).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(srcNested).SelectMany(x=>global::ZLinq.ValueEnumerable.AsValueEnumerable(x),(outer,inner)=>inner+1).Consume(consumer);
    }
}
