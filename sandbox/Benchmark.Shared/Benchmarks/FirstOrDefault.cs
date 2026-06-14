namespace Seamless.Benchmarks;

[BenchmarkTestIterations(320)]
public class FirstOrDefault():Base(20_000_000){
    private const int iterationCount=4_000;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).FirstOrDefault());
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).AsValueEnumerable().FirstOrDefault());
        }
    }
}
