namespace Seamless.Benchmarks;

public class AggregateBy_keySelector():Base(3_310_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);
    }
}
