namespace Seamless.Benchmarks;

public class Zip_tuple():Base(3_790_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).Zip(src2(1)).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().Zip(src2(1)).Consume(this.consumer);
    }
}
