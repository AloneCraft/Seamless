namespace Seamless.Benchmarks;

public class ToDictionary_elementSelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void Intercept(){
        src(1).ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
    }
}
