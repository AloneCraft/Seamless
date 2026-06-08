namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Zip_second:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Zip(this.src,this.src2,(a,b)=>a+b).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Zip(Enumerable.Zip(this.src,this.src2,(a,b)=>a+b),this.src2,(a,b)=>a+b).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Zip(Enumerable.Zip(Enumerable.Zip(this.src,this.src2,(a,b)=>a+b),this.src2,(a,b)=>a+b),this.src2,(a,b)=>a+b).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Zip(Enumerable.Zip(Enumerable.Zip(Enumerable.Zip(this.src,this.src2,(a,b)=>a+b),this.src2,(a,b)=>a+b),this.src2,(a,b)=>a+b),this.src2,(a,b)=>a+b).Consume(this.consumer);
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

    void Intercept1Core(){ this.src.Zip(this.src2,(a,b)=>a+b).Consume(this.consumer); }
    void Intercept2Core(){ this.src.Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Consume(this.consumer); }
    void Intercept3Core(){ this.src.Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Consume(this.consumer); }
    void Intercept4Core(){ this.src.Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        ValueEnumerable.AsValueEnumerable(this.src).Zip(this.src2,(a,b)=>a+b).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        ValueEnumerable.AsValueEnumerable(this.src).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        ValueEnumerable.AsValueEnumerable(this.src).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        ValueEnumerable.AsValueEnumerable(this.src).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Zip(this.src2,(a,b)=>a+b).Consume(this.consumer);
    }
}
