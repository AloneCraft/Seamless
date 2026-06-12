namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DistinctBy_keySelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ1(){
        src(1).DistinctBy(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ2(){
        src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ3(){
        src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [NoLinqRewrite]
    public void LINQ4(){
        src(1).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        this.Intercept1Core();
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        this.Intercept2Core();
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        this.Intercept3Core();
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        this.Intercept4Core();
    }

    void Intercept1Core(){ src(1).AsEnumerable().DistinctBy(x=>x%1000).Consume(this.consumer); }
    void Intercept2Core(){ src(1).AsEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer); }
    void Intercept3Core(){ src(1).AsEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer); }
    void Intercept4Core(){ src(1).AsEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer); }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src(1).AsValueEnumerable().DistinctBy(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src(1).AsValueEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src(1).AsValueEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src(1).AsValueEnumerable().DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(this.consumer);
    }
}
