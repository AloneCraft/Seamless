namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrderBy_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ1(){
        src(1).OrderBy(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ2(){
        src(1).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ3(){
        src(1).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ4(){
        src(1).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer);
    }

    void OrderBy1Core(){ src(1).OrderBy(x=>x).Consume(this.consumer); }
    void OrderBy2Core(){ src(1).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer); }
    void OrderBy3Core(){ src(1).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer); }
    void OrderBy4Core(){ src(1).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer); }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ this.OrderBy1Core(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ this.OrderBy2Core(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ this.OrderBy3Core(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ this.OrderBy4Core(); }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src(1).AsValueEnumerable().OrderBy(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src(1).AsValueEnumerable().OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src(1).AsValueEnumerable().OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src(1).AsValueEnumerable().OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer);
    }
}
