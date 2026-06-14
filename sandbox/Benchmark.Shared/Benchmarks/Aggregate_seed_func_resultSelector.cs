namespace Seamless.Benchmarks;

public class Aggregate_seed_func_resultSelector():Base(7_150_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).Aggregate(0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Aggregate(0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));
    }
}
