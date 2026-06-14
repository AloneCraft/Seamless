namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Reverse():Base(1_800_000){
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Linq)]public void Linq1()=> this.src(1).Reverse().Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq2()=> this.src(1).Reverse().Reverse().Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq3()=> this.src(1).Reverse().Reverse().Reverse().Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq4()=> this.src(1).Reverse().Reverse().Reverse().Reverse().Consume(this.consumer);

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.FLinq)]public void FLinq1()=>this.Linq1();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq2()=>this.Linq2();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq3()=>this.Linq3();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq4()=>this.Linq4();

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.ZLinq)]public void ZLinq1()=> this.src(1).AsValueEnumerable().Reverse().Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq2()=> this.src(1).AsValueEnumerable().Reverse().Reverse().Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq3()=> this.src(1).AsValueEnumerable().Reverse().Reverse().Reverse().Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq4()=> this.src(1).AsValueEnumerable().Reverse().Reverse().Reverse().Reverse().Consume(this.consumer);
}
