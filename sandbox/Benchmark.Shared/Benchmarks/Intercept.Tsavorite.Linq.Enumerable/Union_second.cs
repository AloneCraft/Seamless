using global::Tsavorite.Linq;

namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Union_second():TsavoriteBase(800_000){
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite1()=> this.src(1).Union(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite2()=> this.src(1).Union(this.src2(1)).Union(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite3()=> this.src(1).Union(this.src2(1)).Union(this.src2(1)).Union(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite4()=> this.src(1).Union(this.src2(1)).Union(this.src2(1)).Union(this.src2(1)).Union(this.src2(1)).Consume(this.consumer);

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

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized1()=>this.Tsavorite1();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized2()=>this.Tsavorite2();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized3()=>this.Tsavorite3();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized4()=>this.Tsavorite4();
}
