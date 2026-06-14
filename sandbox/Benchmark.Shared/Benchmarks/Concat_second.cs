namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Concat_second():Base(740_000){
    // System.Linq baseline: リフレクション直呼びのため傍受されず生のまま�E�ELinq と同一連鎖！E
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.Linq)]public void Linq1()=> this.src(1).Concat(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq2()=> this.src(1).Concat(this.src2(1)).Concat(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq3()=> this.src(1).Concat(this.src2(1)).Concat(this.src2(1)).Concat(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.Linq)]public void Linq4()=> this.src(1).Concat(this.src2(1)).Concat(this.src2(1)).Concat(this.src2(1)).Concat(this.src2(1)).Consume(this.consumer);

    // FLinq: this.Linq() 呼び出しが whole-method interception で展開されめE

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.FLinq)]public void FLinq1()=>this.Linq1();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq2()=>this.Linq2();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq3()=>this.Linq3();

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]public void FLinq4()=>this.Linq4();

    // ZLinq: 同一連鎁E+ .AsValueEnumerable() の頭差刁E�Eみ
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.ZLinq)]public void ZLinq1()=> this.src(1).AsValueEnumerable().Concat(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq2()=> this.src(1).AsValueEnumerable().Concat(this.src2(1)).Concat(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq3()=> this.src(1).AsValueEnumerable().Concat(this.src2(1)).Concat(this.src2(1)).Concat(this.src2(1)).Consume(this.consumer);

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]public void ZLinq4()=> this.src(1).AsValueEnumerable().Concat(this.src2(1)).Concat(this.src2(1)).Concat(this.src2(1)).Concat(this.src2(1)).Consume(this.consumer);
}
