namespace Seamless.Benchmarks;

[BenchmarkTestIterations(8)]
public class ElementAt_index():Base(15_430_000){
    private const int iterationCount=3;

    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        const int index=500_000;
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(src(1).ElementAt(index));
        }
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        const int index=500_000;
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(src(1).AsValueEnumerable().ElementAt(index));
        }
    }
}
