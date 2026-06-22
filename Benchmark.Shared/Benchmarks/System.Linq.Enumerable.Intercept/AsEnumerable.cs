using SystemEnumerable=System.Linq.Enumerable;

namespace Benchmark.System.Linq.Enumerable.Intercept;

public class AsEnumerable():Base(16_030_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>SystemEnumerable.AsEnumerable(this.src(1)).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
