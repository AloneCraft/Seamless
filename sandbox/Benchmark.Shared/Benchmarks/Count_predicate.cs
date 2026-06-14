namespace Seamless.Benchmarks;

public class Count_predicate():Base(5_120_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).Count(x=>(x&1)==0));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().Count(x=>(x&1)==0));
}
