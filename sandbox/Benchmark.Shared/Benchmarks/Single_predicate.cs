namespace Seamless.Benchmarks;

public class Single_predicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ() {
        var y=srcOne(1).ToArray();
        var x=y.Single(x => x==0);
        this.consumer.Consume(src(1).Single(x=>x==count/2));
    }

    void InterceptCore(){ this.consumer.Consume(src(1).Single(x=>x==count/2)); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Single(x=>x==count/2));
    }
}
