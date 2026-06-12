namespace Seamless.Benchmarks;

public class Zip_tuple:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).Zip(src2(1)).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).Zip(src2(1)).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().Zip(src2(1)).Consume(this.consumer);
    }
}
