namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrderByDescending_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ1(){
        src(1).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ2(){
        src(1).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ3(){
        src(1).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ4(){
        src(1).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src(1).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src(1).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src(1).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src(1).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src(1).AsValueEnumerable().OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src(1).AsValueEnumerable().OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src(1).AsValueEnumerable().OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src(1).AsValueEnumerable().OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }
}
