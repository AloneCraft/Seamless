namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Union_second:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Union(this.src,this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Union(Enumerable.Union(this.src,this.src2),this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Union(Enumerable.Union(Enumerable.Union(this.src,this.src2),this.src2),this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Union(Enumerable.Union(Enumerable.Union(Enumerable.Union(this.src,this.src2),this.src2),this.src2),this.src2).Consume(this.consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ this.Intercept1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ this.Intercept2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ this.Intercept3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ this.Intercept4Core(); }

    void Intercept1Core(){ this.src.Union(this.src2).Consume(this.consumer); }
    void Intercept2Core(){ this.src.Union(this.src2).Union(this.src2).Consume(this.consumer); }
    void Intercept3Core(){ this.src.Union(this.src2).Union(this.src2).Union(this.src2).Consume(this.consumer); }
    void Intercept4Core(){ this.src.Union(this.src2).Union(this.src2).Union(this.src2).Union(this.src2).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        ValueEnumerable.AsValueEnumerable(this.src).Union(this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        ValueEnumerable.AsValueEnumerable(this.src).Union(this.src2).Union(this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        ValueEnumerable.AsValueEnumerable(this.src).Union(this.src2).Union(this.src2).Union(this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        ValueEnumerable.AsValueEnumerable(this.src).Union(this.src2).Union(this.src2).Union(this.src2).Union(this.src2).Consume(this.consumer);
    }
}
