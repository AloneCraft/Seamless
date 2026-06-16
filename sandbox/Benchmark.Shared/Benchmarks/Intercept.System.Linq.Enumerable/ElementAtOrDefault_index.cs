namespace Benchmark.Intercept.System.Linq.Enumerable;

[BenchmarkTestIterations(18)]
public class ElementAtOrDefault_index():Base(16_340_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).ElementAtOrDefault(500_000));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().ElementAtOrDefault(500_000));

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
