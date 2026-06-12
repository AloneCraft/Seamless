namespace Seamless.Benchmarks;

public class Join_resultSelector_Hoisting:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).Join(src2(1),o => o%1000,i => i%1000,(o,i)=>o+i+HoistInvariant.Value()).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).Join(src2(1),o => o%1000,i => i%1000,(o,i)=>o+i+HoistInvariant.Value()).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().Join(src2(1),o => o%1000,i => i%1000,(o,i)=>o+i+HoistInvariant.Value()).Consume(this.consumer);
    }
}
