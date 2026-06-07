namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrderByDescending_keySelector:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.OrderByDescending(this.src,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.OrderByDescending(Enumerable.OrderByDescending(this.src,x=>x),x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.OrderByDescending(Enumerable.OrderByDescending(Enumerable.OrderByDescending(this.src,x=>x),x=>x),x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.OrderByDescending(Enumerable.OrderByDescending(Enumerable.OrderByDescending(Enumerable.OrderByDescending(this.src,x=>x),x=>x),x=>x),x=>x).Consume(this.consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        this.src.OrderByDescending(x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        this.src.OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        this.src.OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        this.src.OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).OrderByDescending(x=>x).Consume(this.consumer);
    }
}
