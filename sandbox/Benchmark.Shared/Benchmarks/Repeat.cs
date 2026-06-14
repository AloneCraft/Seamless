using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

[BenchmarkTestIterations(16)]
public class Repeat():Base(20_000_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>SystemEnumerable.Repeat(7,1_000_000).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>ValueEnumerable.Repeat(7,1_000_000).Consume(this.consumer);
}
