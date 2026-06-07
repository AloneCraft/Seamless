namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DefaultIfEmpty:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.DefaultIfEmpty(this.src,0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(this.src,0),0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(this.src,0),0),0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(this.src,0),0),0),0).Consume(this.consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        this.src.DefaultIfEmpty(0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        this.src.DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        this.src.DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        this.src.DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(this.consumer);
    }
}
