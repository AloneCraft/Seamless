using global::Tsavorite.Linq;

namespace Benchmark.Tsavorite.Linq.Enumerable.Intercept;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Select_selector():TsavoriteBase(3_200_000){
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite1()=> this.src(1).Select(x=>x+x).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite2()=> this.src(1).Select(x=>x+x).Select(x=>x+x) .Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite3()=> this.src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x) .Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Tsavorite)]public void Tsavorite4()=> this.src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x) .Consume(this.consumer);

    void Select1Core(){ this.src(1).Select(x=>x+x).Consume(this.consumer); }
    void Select2Core(){ this.src(1).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select3Core(){ this.src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select4Core(){ this.src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }

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
