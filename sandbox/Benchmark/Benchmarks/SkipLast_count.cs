namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SkipLast_count:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.SkipLast(src,250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.SkipLast(Enumerable.SkipLast(src,250000),250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.SkipLast(Enumerable.SkipLast(Enumerable.SkipLast(src,250000),250000),250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.SkipLast(Enumerable.SkipLast(Enumerable.SkipLast(Enumerable.SkipLast(src,250000),250000),250000),250000).Consume(consumer);
    }

    void Intercept1Core(){ this.src.SkipLast(250000).Consume(this.consumer); }
    void Intercept2Core(){ this.src.SkipLast(250000).SkipLast(250000).Consume(this.consumer); }
    void Intercept3Core(){ this.src.SkipLast(250000).SkipLast(250000).SkipLast(250000).Consume(this.consumer); }
    void Intercept4Core(){ this.src.SkipLast(250000).SkipLast(250000).SkipLast(250000).SkipLast(250000).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ Intercept1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ Intercept2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ Intercept3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ Intercept4Core(); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).SkipLast(250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).SkipLast(250000).SkipLast(250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).SkipLast(250000).SkipLast(250000).SkipLast(250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).SkipLast(250000).SkipLast(250000).SkipLast(250000).SkipLast(250000).Consume(consumer);
    }
}
