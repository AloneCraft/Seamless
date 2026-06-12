namespace Seamless.Benchmarks;

public class GroupBy_elementSelector_resultSelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);
    }
}
