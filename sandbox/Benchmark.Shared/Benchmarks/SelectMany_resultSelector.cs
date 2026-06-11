namespace Benchmark.Benchmarks;

public class SelectMany_resultSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.SelectMany(this.srcNested,x=>x,(outer,inner)=>inner+1).Consume(this.consumer);
    }

    void InterceptCore(){ this.srcNested.SelectMany(x=>x,(outer,inner)=>inner+1).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.srcNested).SelectMany(x=>ValueEnumerable.AsValueEnumerable(x),(outer,inner)=>inner+1).Consume(this.consumer);
    }
}
