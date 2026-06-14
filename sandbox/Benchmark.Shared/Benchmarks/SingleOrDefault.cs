namespace Seamless.Benchmarks;

[BenchmarkTestIterations(280)]
public class SingleOrDefault():Base(20_000_000){
    private const int iterationCount=4_500;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.srcOne(1).SingleOrDefault());
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.srcOne(1).AsValueEnumerable().SingleOrDefault());
        }
    }
}
