namespace Seamless.Benchmarks;

[BenchmarkTestIterations(350)]
public class First():Base(20_000_000){
    private const int iterationCount=5_000;

    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(src(1).First());
        }
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(src(1).AsValueEnumerable().First());
        }
    }
}
