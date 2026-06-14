namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Zip_second():Base(1_120_000){
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Linq)]public void Linq1()=> this.src(1).Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq2()=> this.src(1).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq3()=> this.src(1).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq4()=> this.src(1).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.FLinq)]public void FLinq1()=>this.Linq1();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq2()=>this.Linq2();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq3()=>this.Linq3();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq4()=>this.Linq4();

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.ZLinq)]public void ZLinq1()=> this.src(1).AsValueEnumerable().Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq2()=> this.src(1).AsValueEnumerable().Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq3()=> this.src(1).AsValueEnumerable().Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq4()=> this.src(1).AsValueEnumerable().Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);
}
