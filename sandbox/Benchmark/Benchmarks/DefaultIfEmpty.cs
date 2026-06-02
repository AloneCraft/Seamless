namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DefaultIfEmpty:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.DefaultIfEmpty(src,0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(src,0),0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(src,0),0),0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(Enumerable.DefaultIfEmpty(src,0),0),0),0).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src.DefaultIfEmpty(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable().DefaultIfEmpty(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable().DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable().DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable().DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).DefaultIfEmpty(0).Consume(consumer);
    }
}
