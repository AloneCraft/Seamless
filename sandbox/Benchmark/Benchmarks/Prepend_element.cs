namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Prepend_element:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Prepend(src,0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Prepend(Enumerable.Prepend(src,0),0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Prepend(Enumerable.Prepend(Enumerable.Prepend(src,0),0),0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Prepend(Enumerable.Prepend(Enumerable.Prepend(Enumerable.Prepend(src,0),0),0),0).Consume(consumer);
    }

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

    void Intercept1Core(){ this.src.AsEnumerable().Prepend(0).Consume(this.consumer); }
    void Intercept2Core(){ this.src.AsEnumerable().Prepend(0).Prepend(0).Consume(this.consumer); }
    void Intercept3Core(){ this.src.AsEnumerable().Prepend(0).Prepend(0).Prepend(0).Consume(this.consumer); }
    void Intercept4Core(){ this.src.AsEnumerable().Prepend(0).Prepend(0).Prepend(0).Prepend(0).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Prepend(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Prepend(0).Prepend(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Prepend(0).Prepend(0).Prepend(0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Prepend(0).Prepend(0).Prepend(0).Prepend(0).Consume(consumer);
    }
}
