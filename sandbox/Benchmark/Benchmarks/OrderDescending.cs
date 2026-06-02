namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrderDescending:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.OrderDescending(src).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.OrderDescending(Enumerable.OrderDescending(src)).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.OrderDescending(Enumerable.OrderDescending(Enumerable.OrderDescending(src))).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.OrderDescending(Enumerable.OrderDescending(Enumerable.OrderDescending(Enumerable.OrderDescending(src)))).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src.OrderDescending().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.OrderDescending().OrderDescending().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.OrderDescending().OrderDescending().OrderDescending().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.OrderDescending().OrderDescending().OrderDescending().OrderDescending().Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable().OrderDescending().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable().OrderDescending().OrderDescending().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable().OrderDescending().OrderDescending().OrderDescending().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable().OrderDescending().OrderDescending().OrderDescending().OrderDescending().Consume(consumer);
    }
}
