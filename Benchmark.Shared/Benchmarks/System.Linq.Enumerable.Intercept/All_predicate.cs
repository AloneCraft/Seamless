namespace Benchmark.System.Linq.Enumerable.Intercept;

public class All_predicate():Base(5_330_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).All(x=>x>0));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().All(x=>x>0));

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
