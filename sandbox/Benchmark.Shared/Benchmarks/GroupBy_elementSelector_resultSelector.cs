namespace Seamless.Benchmarks;

public class GroupBy_elementSelector_resultSelector():Base(1_600_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);
}
