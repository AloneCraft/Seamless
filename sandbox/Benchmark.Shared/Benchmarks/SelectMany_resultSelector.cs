namespace Seamless.Benchmarks;

public class SelectMany_resultSelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        srcNested(1).SelectMany(x=>x,(outer,inner)=>inner+1).Consume(this.consumer);
    }

    void InterceptCore(){ srcNested(1).SelectMany(x=>x,(outer,inner)=>inner+1).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        srcNested(1).AsValueEnumerable().SelectMany(x=>x.AsValueEnumerable(),(outer,inner)=>inner+1).Consume(this.consumer);
    }
}
