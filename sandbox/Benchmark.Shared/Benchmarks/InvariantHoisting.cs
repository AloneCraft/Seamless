namespace Seamless.Benchmarks;

public class InvariantHoisting:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).Select(x=>x+HoistInvariant.Value()).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);
    }
}
