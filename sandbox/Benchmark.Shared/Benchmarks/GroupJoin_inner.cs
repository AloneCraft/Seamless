namespace Seamless.Benchmarks;

public class GroupJoin_inner():Base(700_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).GroupJoin(this.src2(1),x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().GroupJoin(this.src2(1),x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer);
}
