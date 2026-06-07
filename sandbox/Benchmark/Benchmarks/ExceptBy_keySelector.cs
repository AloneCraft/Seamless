namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ExceptBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.ExceptBy(this.src,this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.ExceptBy(Enumerable.ExceptBy(this.src,this.src2,x=>x),this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.ExceptBy(Enumerable.ExceptBy(Enumerable.ExceptBy(this.src,this.src2,x=>x),this.src2,x=>x),this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.ExceptBy(Enumerable.ExceptBy(Enumerable.ExceptBy(Enumerable.ExceptBy(this.src,this.src2,x=>x),this.src2,x=>x),this.src2,x=>x),this.src2,x=>x).Consume(this.consumer);
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

    void Intercept1Core(){ this.src.ExceptBy(this.src2,x=>x).Consume(this.consumer); }
    void Intercept2Core(){ this.src.ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).Consume(this.consumer); }
    void Intercept3Core(){ this.src.ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).Consume(this.consumer); }
    void Intercept4Core(){ this.src.ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ExceptBy(this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).ExceptBy(this.src2,x=>x).Consume(this.consumer);
    }
}
