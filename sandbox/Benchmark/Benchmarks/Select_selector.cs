namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Select_selector:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Select(this.src,x=>x+x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Select(Enumerable.Select(this.src,x=>x+x),x=>x+x)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Select(Enumerable.Select(Enumerable.Select(this.src,x=>x+x),x=>x+x),x=>x+x)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Select(Enumerable.Select(Enumerable.Select(Enumerable.Select(this.src,x=>x+x),x=>x+x),x=>x+x),x=>x+x)
           .Consume(this.consumer);
    }

    void Select1Core(){ this.src.Select(x=>x+x).Consume(this.consumer); }
    void Select2Core(){ this.src.Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select3Core(){ this.src.Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select4Core(){ this.src.Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ this.Select1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ this.Select2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ this.Select3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ this.Select4Core(); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src)
           .Select(x=>x+x)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(this.consumer);
    }
}
