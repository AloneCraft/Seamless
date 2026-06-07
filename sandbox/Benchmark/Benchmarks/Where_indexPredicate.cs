namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Where_indexPredicate:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Where(src,(x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Where(Enumerable.Where(src,(x,i)=>i%2==0),(x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Where(Enumerable.Where(Enumerable.Where(src,(x,i)=>i%2==0),(x,i)=>i%2==0),(x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Where(Enumerable.Where(Enumerable.Where(Enumerable.Where(src,(x,i)=>i%2==0),(x,i)=>i%2==0),(x,i)=>i%2==0),(x,i)=>i%2==0).Consume(consumer);
    }

    void Intercept1Core(){ this.src.Where((x,i)=>i%2==0).Consume(this.consumer); }
    void Intercept2Core(){ this.src.Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(this.consumer); }
    void Intercept3Core(){ this.src.Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(this.consumer); }
    void Intercept4Core(){ this.src.Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(this.consumer); }

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
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Where((x,i)=>i%2==0).Consume(consumer);
    }
}
