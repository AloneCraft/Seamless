namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DefaultIfEmpty:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ1(){
        src(1).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ2(){
        src(1).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ3(){
        src(1).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ4(){
        src(1).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src(1).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src(1).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src(1).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src(1).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src(1).AsValueEnumerable().DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src(1).AsValueEnumerable().DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src(1).AsValueEnumerable().DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src(1).AsValueEnumerable().DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }
}
