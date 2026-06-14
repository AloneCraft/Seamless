namespace Seamless.Benchmarks;

public class SelectMany_indexSelector_resultSelector():Base(2_290_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.srcNested(1).SelectMany((x,i)=>x,(outer,inner)=>inner+1).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.srcNested(1).AsValueEnumerable().SelectMany((x,i)=>x.AsValueEnumerable(),(outer,inner)=>inner+1).Consume(this.consumer);
}
