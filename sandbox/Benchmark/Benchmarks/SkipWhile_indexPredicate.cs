namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SkipWhile_indexPredicate:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.SkipWhile(src,(x,i)=>i<250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.SkipWhile(Enumerable.SkipWhile(src,(x,i)=>i<250000),(x,i)=>i<250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.SkipWhile(Enumerable.SkipWhile(Enumerable.SkipWhile(src,(x,i)=>i<250000),(x,i)=>i<250000),(x,i)=>i<250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.SkipWhile(Enumerable.SkipWhile(Enumerable.SkipWhile(Enumerable.SkipWhile(src,(x,i)=>i<250000),(x,i)=>i<250000),(x,i)=>i<250000),(x,i)=>i<250000).Consume(consumer);
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

    void Intercept1Core(){ this.src.AsEnumerable().SkipWhile((x,i)=>i<250000).Consume(this.consumer); }
    void Intercept2Core(){ this.src.AsEnumerable().SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).Consume(this.consumer); }
    void Intercept3Core(){ this.src.AsEnumerable().SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).Consume(this.consumer); }
    void Intercept4Core(){ this.src.AsEnumerable().SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).SkipWhile((x,i)=>i<250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).SkipWhile((x,i)=>i<250000).Consume(consumer);
    }
}
