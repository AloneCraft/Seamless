namespace Seamless.Benchmarks;

public class ElementAt_index:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).ElementAt(500000));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).ElementAt(500000)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().ElementAt(500000));
    }
}
