using global::Tsavorite.Linq;


namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DistinctBy_keySelector():TsavoriteBase(1_800_000){
    private int anchorKeyCalls;

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite1()=> this.src(1).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite2()=> this.src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite3()=> this.src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite4()=> this.src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.FLinq)]public void FLinq1()=>this.Tsavorite1();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq2()=>this.Tsavorite2();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq3()=>this.Tsavorite3();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq4()=>this.Tsavorite4();





    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match(){
        this.AssertResults(this.Tsavorite1,this.FLinq1,this.FLinqNoOptimized1);
        this.AssertResults(this.Tsavorite2,this.FLinq2,this.FLinqNoOptimized2);
        this.AssertResults(this.Tsavorite3,this.FLinq3,this.FLinqNoOptimized3);
        this.AssertResults(this.Tsavorite4,this.FLinq4,this.FLinqNoOptimized4);
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
    private void FLinqNoOptimized1()=>this.Tsavorite1();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized2()=>this.Tsavorite2();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized3()=>this.Tsavorite3();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized4()=>this.Tsavorite4();
}
