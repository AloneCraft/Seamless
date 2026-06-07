namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Intersect_second:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Intersect(this.src,this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Intersect(Enumerable.Intersect(this.src,this.src2),this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Intersect(Enumerable.Intersect(Enumerable.Intersect(this.src,this.src2),this.src2),this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Intersect(Enumerable.Intersect(Enumerable.Intersect(Enumerable.Intersect(this.src,this.src2),this.src2),this.src2),this.src2).Consume(this.consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ this.Intercept1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ this.Intercept2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ this.Intercept3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ this.Intercept4Core(); }

    void Intercept1Core(){ this.src.Intersect(this.src2).Consume(this.consumer); }
    void Intercept2Core(){ this.src.Intersect(this.src2).Intersect(this.src2).Consume(this.consumer); }
    void Intercept3Core(){ this.src.Intersect(this.src2).Intersect(this.src2).Intersect(this.src2).Consume(this.consumer); }
    void Intercept4Core(){ this.src.Intersect(this.src2).Intersect(this.src2).Intersect(this.src2).Intersect(this.src2).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Intersect(this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Intersect(this.src2).Intersect(this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Intersect(this.src2).Intersect(this.src2).Intersect(this.src2).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Intersect(this.src2).Intersect(this.src2).Intersect(this.src2).Intersect(this.src2).Consume(this.consumer);
    }
}
