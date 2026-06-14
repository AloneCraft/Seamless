using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

[BenchmarkTestIterations(17)]
public class Range():Base(11_200_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>SystemEnumerable.Range(1,1_000_000).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>ValueEnumerable.Range(1,1_000_000).Consume(this.consumer);
}
