namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Take_count:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Take(src,750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Take(Enumerable.Take(src,750000),750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Take(Enumerable.Take(Enumerable.Take(src,750000),750000),750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Take(Enumerable.Take(Enumerable.Take(Enumerable.Take(src,750000),750000),750000),750000).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src.Take(750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.Take(750000).Take(750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.Take(750000).Take(750000).Take(750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.Take(750000).Take(750000).Take(750000).Take(750000).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable().Take(750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable().Take(750000).Take(750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable().Take(750000).Take(750000).Take(750000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable().Take(750000).Take(750000).Take(750000).Take(750000).Consume(consumer);
    }
}
