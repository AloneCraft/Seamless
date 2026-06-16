using global::Tsavorite.Linq;

namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Skip_count():TsavoriteBase(3_840_000){
#if BENCH_AS_TEST
    private const int SkipCount=1_000;
#else
    private const int SkipCount=250_000;
#endif

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite1()=> this.src(1).Skip(SkipCount).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite2()=> this.src(1).Skip(SkipCount).Skip(SkipCount).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite3()=> this.src(1).Skip(SkipCount).Skip(SkipCount).Skip(SkipCount).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite4()=> this.src(1).Skip(SkipCount).Skip(SkipCount).Skip(SkipCount).Skip(SkipCount).Consume(this.consumer);

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
