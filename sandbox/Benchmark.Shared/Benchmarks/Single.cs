namespace Seamless.Benchmarks;

public class Single:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(srcOne(1).Single());
    }

    void InterceptCore(){ this.consumer.Consume(srcOne(1).Single()); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(srcOne(1).AsValueEnumerable().Single());
    }
}
