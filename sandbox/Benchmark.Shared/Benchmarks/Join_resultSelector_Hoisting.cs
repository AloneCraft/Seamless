namespace Seamless.Benchmarks;

public class Join_resultSelector_Hoisting:Base{
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).Join(this.src2(1),o => o%1000,i => i%1000,(o,i)=>o+i+HoistInvariant.Value()).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().Join(this.src2(1),o => o%1000,i => i%1000,(o,i)=>o+i+HoistInvariant.Value()).Consume(this.consumer);
}
