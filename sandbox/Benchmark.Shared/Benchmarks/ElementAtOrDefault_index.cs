namespace Seamless.Benchmarks;

public class ElementAtOrDefault_index:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).ElementAtOrDefault(500000));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).ElementAtOrDefault(500000)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().ElementAtOrDefault(500000));
    }
}
