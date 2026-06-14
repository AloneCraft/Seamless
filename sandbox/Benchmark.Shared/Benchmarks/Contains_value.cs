namespace Seamless.Benchmarks;

[BenchmarkTestIterations(7)]
public class Contains_value():Base(10_630_000){
    private const int iterationCount=3;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        const int value=500_000;
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).Contains(value));
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        const int value=500_000;
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).AsValueEnumerable().Contains(value));
        }
    }
}
