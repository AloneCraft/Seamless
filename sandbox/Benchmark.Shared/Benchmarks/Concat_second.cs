namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Concat_second:Base{
    // System.Linq baseline: リフレクション直呼びのため傍受されず生のまま（FLinq と同一連鎖）
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){ src(1).Concat(src2(1)).Consume(this.consumer); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){ src(1).Concat(src2(1)).Concat(src2(1)).Consume(this.consumer); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){ src(1).Concat(src2(1)).Concat(src2(1)).Concat(src2(1)).Consume(this.consumer); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){ src(1).Concat(src2(1)).Concat(src2(1)).Concat(src2(1)).Concat(src2(1)).Consume(this.consumer); }

    // FLinq: this.LINQ() 呼び出しが whole-method interception で展開される

    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.FLinq)]
    public void FLinq1(){ this.LINQ1(); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]
    public void FLinq2(){ this.LINQ2(); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]
    public void FLinq3(){ this.LINQ3(); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.FLinq)]
    public void FLinq4(){ this.LINQ4(); }

    // ZLinq: 同一連鎖 + .AsValueEnumerable() の頭差分のみ
    [BenchFact,Benchmark(Baseline=true),BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){ src(1).AsValueEnumerable().Concat(src2(1)).Consume(this.consumer); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){ src(1).AsValueEnumerable().Concat(src2(1)).Concat(src2(1)).Consume(this.consumer); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){ src(1).AsValueEnumerable().Concat(src2(1)).Concat(src2(1)).Concat(src2(1)).Consume(this.consumer); }

    [BenchFact,Benchmark,BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){ src(1).AsValueEnumerable().Concat(src2(1)).Concat(src2(1)).Concat(src2(1)).Concat(src2(1)).Consume(this.consumer); }
}
