namespace Seamless.Benchmarks;

public class SelectMany_resultSelector():Base(2_000_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.srcNested(1).SelectMany(x=>x,(outer,inner)=>inner+1).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.srcNested(1).AsValueEnumerable().SelectMany(x=>x.AsValueEnumerable(),(outer,inner)=>inner+1).Consume(this.consumer);
}
