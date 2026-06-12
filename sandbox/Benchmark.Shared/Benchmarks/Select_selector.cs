namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Select_selector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ1(){
        src(1).Select(x=>x+x).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ2(){
        src(1).Select(x=>x+x).Select(x=>x+x)
           .Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ3(){
        src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x)
           .Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ4(){
        src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x)
           .Consume(this.consumer);
    }

    void Select1Core(){ src(1).Select(x=>x+x).Consume(this.consumer); }
    void Select2Core(){ src(1).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select3Core(){ src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select4Core(){ src(1).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ this.Select1Core(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ this.Select2Core(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ this.Select3Core(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ this.Select4Core(); }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src(1).AsValueEnumerable()
           .Select(x=>x+x)
           .Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src(1).AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src(1).AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src(1).AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);
    }
}
