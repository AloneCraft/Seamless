namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IntersectBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.IntersectBy(this.src,this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.IntersectBy(Enumerable.IntersectBy(this.src,this.src2,x=>x),this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.IntersectBy(Enumerable.IntersectBy(Enumerable.IntersectBy(this.src,this.src2,x=>x),this.src2,x=>x),this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.IntersectBy(Enumerable.IntersectBy(Enumerable.IntersectBy(Enumerable.IntersectBy(this.src,this.src2,x=>x),this.src2,x=>x),this.src2,x=>x),this.src2,x=>x).Consume(this.consumer);
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

    void Intercept1Core(){ this.src.IntersectBy(this.src2,x=>x).Consume(this.consumer); }
    void Intercept2Core(){ this.src.IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).Consume(this.consumer); }
    void Intercept3Core(){ this.src.IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).Consume(this.consumer); }
    void Intercept4Core(){ this.src.IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).IntersectBy(this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).Consume(this.consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).IntersectBy(this.src2,x=>x).Consume(this.consumer);
    }
}
