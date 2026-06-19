using Tsavorite.Linq;

namespace Benchmark.System.Linq.Enumerable.Intercept;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DistinctBy_keySelector():Base(1_800_000){
    private int anchorKeyCalls;

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Linq)]public void Linq1()=> this.src(1).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq2()=> this.src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq3()=> this.src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq4()=> this.src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.FLinq)]public void FLinq1()=>this.Linq1();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq2()=>this.Linq2();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq3()=>this.Linq3();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq4()=>this.Linq4();

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.ZLinq)]public void ZLinq1()=> this.src(1).AsValueEnumerable().DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq2()=> this.src(1).AsValueEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq3()=> this.src(1).AsValueEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq4()=> this.src(1).AsValueEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match(){
        this.AssertResults(this.Linq1,this.FLinq1,this.FLinqNoOptimized1,this.ZLinq1);
        this.AssertResults(this.Linq2,this.FLinq2,this.FLinqNoOptimized2,this.ZLinq2);
        this.AssertResults(this.Linq3,this.FLinq3,this.FLinqNoOptimized3,this.ZLinq3);
        this.AssertResults(this.Linq4,this.FLinq4,this.FLinqNoOptimized4,this.ZLinq4);
    }

#if BENCH_AS_TEST
    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Anchor_preserves_selector_evaluation_count(){
        this.anchorKeyCalls=0;
        this.AnchorOptimized();

        Xunit.Assert.Equal(this.count+1000,this.anchorKeyCalls);
    }
#endif

    private int CountedAnchorKey(int value){
        this.anchorKeyCalls++;
        return value%1000;
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void AnchorOptimized()=>this.src(1)
        .DistinctBy(x=>this.CountedAnchorKey(x).Anchor)
        .DistinctBy(x=>this.CountedAnchorKey(x).Anchor)
        .Consume(this.consumer);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized1()=>this.Linq1();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized2()=>this.Linq2();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized3()=>this.Linq3();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized4()=>this.Linq4();
}
