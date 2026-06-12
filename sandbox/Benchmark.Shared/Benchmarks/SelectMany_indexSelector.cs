namespace Seamless.Benchmarks;

public class SelectMany_indexSelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        srcNested(1).SelectMany((x,i)=>x).Consume(this.consumer);
    }

    void InterceptCore(){ srcNested(1).SelectMany((x,i)=>x).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        srcNested(1).AsValueEnumerable().SelectMany((x,i)=>x.AsValueEnumerable()).Consume(this.consumer);
    }
}
