namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Order():Base(280_000){
    [BenchFact]
    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    // 蜊倡峡 Order() 繧・Pipeline 邨瑚ｷｯ・域￡遲峨く繝ｼ鬆・ｺ擾ｼ峨〒螻暮幕縺輔ｌ繧九◆繧√：Linq1 縺ｯ譛驕ｩ蛹悶＆繧後ｋ縲・
    public void LINQ1() {
        src(1).Order().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2() {
        src(1).Order().Order().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3() {
        src(1).Order().Order().Order().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4() {
        src(1).Order().Order().Order().Order().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.FLinq)]
    public void FLinq1() { this.LINQ1(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.FLinq)]
    public void FLinq2() { this.LINQ2(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.FLinq)]
    public void FLinq3() { this.LINQ3(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.FLinq)]
    public void FLinq4() { this.LINQ4(); }

    [BenchFact]
    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1() {
        src(1).AsValueEnumerable().Order().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2() {
        src(1).AsValueEnumerable().Order().Order().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3() {
        src(1).AsValueEnumerable().Order().Order().Order().Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4() {
        src(1).AsValueEnumerable().Order().Order().Order().Order().Consume(this.consumer);
    }
}
