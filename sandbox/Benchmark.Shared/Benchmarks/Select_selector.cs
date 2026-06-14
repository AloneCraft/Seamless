namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Select_selector():Base(3_200_000){
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Linq)]public void Linq1()=> this.src(1).Select(x=>x+x).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq2()=> this.src(1).Select(x=>x+x).Select(x=>x+x) .Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq3()=> this.src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x) .Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq4()=> this.src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x) .Consume(this.consumer);

    void Select1Core(){ this.src(1).Select(x=>x+x).Consume(this.consumer); }
    void Select2Core(){ this.src(1).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select3Core(){ this.src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select4Core(){ this.src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.FLinq)]public void FLinq1()=>this.Linq1();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq2()=>this.Linq2();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq3()=>this.Linq3();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq4()=>this.Linq4();

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.ZLinq)]public void ZLinq1()=> this.src(1).AsValueEnumerable()
           .Select(x=>x+x)
           .Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq2()=> this.src(1).AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq3()=> this.src(1).AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq4()=> this.src(1).AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);
}
