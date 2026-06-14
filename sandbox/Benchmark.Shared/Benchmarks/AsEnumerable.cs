using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

public class AsEnumerable():Base(16_030_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        SystemEnumerable.AsEnumerable(src(1)).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }
}
