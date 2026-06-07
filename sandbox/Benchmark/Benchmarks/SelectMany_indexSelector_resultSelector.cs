namespace Benchmark.Benchmarks;

public class SelectMany_indexSelector_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(this.srcNested,(x,i)=>x,(outer,inner)=>inner+1).Consume(this.consumer);
    }

    void InterceptCore(){ this.srcNested.SelectMany((x,i)=>x,(outer,inner)=>inner+1).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.srcNested).SelectMany((x,i)=>global::ZLinq.ValueEnumerable.AsValueEnumerable(x),(outer,inner)=>inner+1).Consume(this.consumer);
    }
}
