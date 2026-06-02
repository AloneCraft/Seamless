namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrderBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.OrderBy(src,x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.OrderBy(Enumerable.OrderBy(src,x=>x),x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.OrderBy(Enumerable.OrderBy(Enumerable.OrderBy(src,x=>x),x=>x),x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.OrderBy(Enumerable.OrderBy(Enumerable.OrderBy(Enumerable.OrderBy(src,x=>x),x=>x),x=>x),x=>x).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src.OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable().OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable().OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable().OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable().OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }
}
