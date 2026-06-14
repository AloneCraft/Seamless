namespace Seamless.Benchmarks;

[BenchmarkTestIterations(280)]
public class SingleOrDefault():Base(20_000_000){
    private const int iterationCount=4_500;

    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(srcOne(1).SingleOrDefault());
        }
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(srcOne(1).AsValueEnumerable().SingleOrDefault());
        }
    }
}
