namespace Seamless.Benchmarks;

public class SelectMany_indexSelector():Base(2_800_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.srcNested(1).SelectMany((x,i)=>x).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.srcNested(1).AsValueEnumerable().SelectMany((x,i)=>x.AsValueEnumerable()).Consume(this.consumer);
}
