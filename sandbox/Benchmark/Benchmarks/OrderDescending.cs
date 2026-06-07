namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrderDescending:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.OrderDescending(this.src).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.OrderDescending(Enumerable.OrderDescending(this.src)).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.OrderDescending(Enumerable.OrderDescending(Enumerable.OrderDescending(this.src))).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.OrderDescending(Enumerable.OrderDescending(Enumerable.OrderDescending(Enumerable.OrderDescending(this.src)))).Consume(this.consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        this.src.OrderDescending().Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        this.src.OrderDescending().OrderDescending().Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        this.src.OrderDescending().OrderDescending().OrderDescending().Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        this.src.OrderDescending().OrderDescending().OrderDescending().OrderDescending().Consume(this.consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderDescending().Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderDescending().OrderDescending().Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderDescending().OrderDescending().OrderDescending().Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderDescending().OrderDescending().OrderDescending().OrderDescending().Consume(this.consumer);
    }
}
