namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Select_selector:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Select(src,x=>x+x).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Select(Enumerable.Select(src,x=>x+x),x=>x+x)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Select(Enumerable.Select(Enumerable.Select(src,x=>x+x),x=>x+x),x=>x+x)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Select(Enumerable.Select(Enumerable.Select(Enumerable.Select(src,x=>x+x),x=>x+x),x=>x+x),x=>x+x)
           .Consume(consumer);
    }

    void Select1Core(){ this.src.Select(x=>x+x).Consume(this.consumer); }
    void Select2Core(){ this.src.Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select3Core(){ this.src.Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }
    void Select4Core(){ this.src.Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Select(x=>x+x).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ Select1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ Select2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ Select3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ Select4Core(); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable()
           .Select(x=>x+x)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable()
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Select(x=>x+x)
           .Consume(consumer);
    }
}
