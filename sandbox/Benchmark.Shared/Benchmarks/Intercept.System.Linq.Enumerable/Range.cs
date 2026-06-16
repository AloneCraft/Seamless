using SystemEnumerable=System.Linq.Enumerable;

namespace Benchmark.Intercept.System.Linq.Enumerable;

[BenchmarkTestIterations(17)]
public class Range():Base(11_200_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>SystemEnumerable.Range(1,1_000_000).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>ValueEnumerable.Range(1,1_000_000).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
