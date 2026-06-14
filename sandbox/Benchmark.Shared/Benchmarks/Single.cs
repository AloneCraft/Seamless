namespace Seamless.Benchmarks;

[BenchmarkTestIterations(240)]
public class Single():Base(20_000_000){
    private const int iterationCount=6_000;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.srcOne(1).Single());
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.srcOne(1).AsValueEnumerable().Single());
        }
    }
}
