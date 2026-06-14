using SystemEnumerable=System.Linq.Enumerable;

namespace Seamless.Benchmarks;

[BenchmarkTestIterations(650)]
public class Empty():Base(20_000_000){
    private const int iterationCount=25_000;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        for(var i=0;i<iterationCount;i++){
            SystemEnumerable.Empty<int>().Consume(this.consumer);
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            ValueEnumerable.Empty<int>().Consume(this.consumer);
        }
    }
}
