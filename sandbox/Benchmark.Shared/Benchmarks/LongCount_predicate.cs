namespace Seamless.Benchmarks;

public class LongCount_predicate():Base(4_780_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).LongCount(x=>(x&1)==0));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().LongCount(x=>(x&1)==0));
    }
}
