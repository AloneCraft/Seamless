namespace Benchmark.System.Linq.Enumerable.Intercept;

public class AggregateBy_keySelector():Base(3_310_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
