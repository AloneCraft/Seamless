namespace Seamless.Benchmarks;

public class SequenceEqual_second:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).SequenceEqual(src(1)));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).SequenceEqual(src(1))); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().SequenceEqual(src(1)));
    }
}
