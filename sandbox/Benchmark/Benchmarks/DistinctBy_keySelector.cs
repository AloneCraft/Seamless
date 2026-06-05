namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DistinctBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.DistinctBy(src,x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.DistinctBy(Enumerable.DistinctBy(src,x=>x%1000),x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.DistinctBy(Enumerable.DistinctBy(Enumerable.DistinctBy(src,x=>x%1000),x=>x%1000),x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.DistinctBy(Enumerable.DistinctBy(Enumerable.DistinctBy(Enumerable.DistinctBy(src,x=>x%1000),x=>x%1000),x=>x%1000),x=>x%1000).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src.DistinctBy(x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).DistinctBy(x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).DistinctBy(x=>x%1000).Consume(consumer);
    }
}
