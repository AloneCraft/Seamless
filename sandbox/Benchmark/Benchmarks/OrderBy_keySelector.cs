namespace Benchmark.Benchmarks;

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

    void OrderBy1Core(){ this.src.OrderBy(x=>x).Consume(this.consumer); }
    void OrderBy2Core(){ this.src.OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer); }
    void OrderBy3Core(){ this.src.OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer); }
    void OrderBy4Core(){ this.src.OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ OrderBy1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ OrderBy2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ OrderBy3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ OrderBy4Core(); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).OrderBy(x=>x).Consume(consumer);
    }
}
